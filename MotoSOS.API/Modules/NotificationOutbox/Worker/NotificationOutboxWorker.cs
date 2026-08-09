using System.Globalization;
using Microsoft.Extensions.Options;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Modules.AuditLogs.Application;
using MotoSOS.API.Modules.AuditLogs.Domain;
using MotoSOS.API.Modules.NotificationOutbox.Application;
using MotoSOS.API.Modules.NotificationOutbox.Contracts;

namespace MotoSOS.API.Modules.NotificationOutbox.Worker;

public sealed class NotificationOutboxWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<NotificationOutboxWorkerOptions> _options;
    private readonly INotificationOutboxWorkerStateStore _state;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _runLock = new(1, 1);

    public NotificationOutboxWorker(IServiceScopeFactory scopeFactory, IOptions<NotificationOutboxWorkerOptions> options, INotificationOutboxWorkerStateStore state, IClock clock)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _state = state;
        _clock = clock;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        NotificationOutboxWorkerOptions options = _options.Value;
        if (!options.Enabled)
        {
            return;
        }

        if (options.RunOnStartup)
        {
            await RunOnceAsync(stoppingToken);
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, options.IntervalSeconds)));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        NotificationOutboxWorkerOptions options = _options.Value;
        if (!options.Enabled)
        {
            return;
        }

        bool lockTaken = false;
        try
        {
            if (!await _runLock.WaitAsync(0, cancellationToken))
            {
                DateTimeOffset skippedAtUtc = _clock.UtcNow;
                _state.MarkSkipped(skippedAtUtc);
                await RecordWorkerAuditAsync(AuditAction.NotificationOutboxWorkerSkipped, 0, 0, 0, 1, null, cancellationToken);
                return;
            }

            lockTaken = true;
            DateTimeOffset startedAtUtc = _clock.UtcNow;
            _state.MarkStarted(startedAtUtc);
            using IServiceScope scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<INotificationOutboxService>();
            RunNotificationOutboxResponse response = await service.RunWorkerAsync(new RunNotificationOutboxRequest(options.MaxItemsPerRun, options.SimulateFailures), options.IntervalSeconds, cancellationToken);
            _state.MarkSucceeded(_clock.UtcNow, response.Processed, response.SimulatedSent, response.Failed, response.Skipped);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _state.MarkFailed(_clock.UtcNow, "worker_cancelled", "Worker execution was cancelled.");
        }
        catch
        {
            _state.MarkFailed(_clock.UtcNow, "worker_run_failed", "Worker execution failed in a controlled way.");
            await RecordWorkerAuditAsync(AuditAction.NotificationOutboxWorkerFailed, 0, 0, 0, 0, "worker_run_failed", CancellationToken.None);
        }
        finally
        {
            if (lockTaken) _runLock.Release();
        }
    }

    private async Task RecordWorkerAuditAsync(AuditAction action, int processed, int simulatedSent, int failed, int skipped, string? reason, CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            IAuditLogService? auditLogs = scope.ServiceProvider.GetService<IAuditLogService>();
            if (auditLogs is null) return;
            NotificationOutboxWorkerOptions options = _options.Value;
            await auditLogs.RecordAsync("notification-outbox-worker", "System", action, AuditModule.NotificationOutbox, "NotificationOutboxWorker", null, AuditOutcome.Success, reason, null, null, new Dictionary<string, string> { ["processed"] = processed.ToString(CultureInfo.InvariantCulture), ["simulatedSent"] = simulatedSent.ToString(CultureInfo.InvariantCulture), ["failed"] = failed.ToString(CultureInfo.InvariantCulture), ["skipped"] = skipped.ToString(CultureInfo.InvariantCulture), ["maxItems"] = options.MaxItemsPerRun.ToString(CultureInfo.InvariantCulture), ["simulateFailures"] = options.SimulateFailures.ToString(), ["intervalSeconds"] = options.IntervalSeconds.ToString(CultureInfo.InvariantCulture), ["runSource"] = "Worker" }, cancellationToken);
        }
        catch
        {
        }
    }
}
