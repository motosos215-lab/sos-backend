using System.Globalization;
using Microsoft.Extensions.Options;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Modules.AuditLogs.Application;
using MotoSOS.API.Modules.AuditLogs.Domain;
using MotoSOS.API.Modules.Escalations.Application;
using MotoSOS.API.Modules.Escalations.Contracts;

namespace MotoSOS.API.Modules.Escalations.Worker;

public sealed class AutomaticEscalationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<AutomaticEscalationWorkerOptions> _options;
    private readonly IAutomaticEscalationWorkerStateStore _state;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _runLock = new(1, 1);

    public AutomaticEscalationWorker(IServiceScopeFactory scopeFactory, IOptions<AutomaticEscalationWorkerOptions> options, IAutomaticEscalationWorkerStateStore state, IClock clock)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _state = state;
        _clock = clock;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        AutomaticEscalationWorkerOptions options = _options.Value;
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
        AutomaticEscalationWorkerOptions options = _options.Value;
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
                await RecordWorkerAuditAsync(AuditAction.AutomaticEscalationWorkerSkipped, null, "automatic_escalation_worker_overlap", cancellationToken);
                return;
            }

            lockTaken = true;
            _state.MarkStarted(_clock.UtcNow);
            using IServiceScope scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IAutomaticEscalationService>();
            RunAutomaticEscalationResponse response = await service.RunAsync(new RunAutomaticEscalationRequest(options.MaxItemsPerRun, options.EscalateAfterSeconds), "Worker", cancellationToken);
            _state.MarkSucceeded(_clock.UtcNow, response.Processed, response.Escalated, response.Skipped, response.AlreadyEscalated, response.AlreadyAcknowledged, response.NotReady, response.Failed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _state.MarkFailed(_clock.UtcNow, "automatic_escalation_worker_cancelled", "Worker execution was cancelled.");
        }
        catch
        {
            _state.MarkFailed(_clock.UtcNow, "automatic_escalation_worker_failed", "Worker execution failed in a controlled way.");
            await RecordWorkerAuditAsync(AuditAction.AutomaticEscalationWorkerFailed, null, "automatic_escalation_worker_failed", CancellationToken.None);
        }
        finally
        {
            if (lockTaken) _runLock.Release();
        }
    }

    private async Task RecordWorkerAuditAsync(AuditAction action, RunAutomaticEscalationResponse? response, string? reason, CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            IAuditLogService? auditLogs = scope.ServiceProvider.GetService<IAuditLogService>();
            if (auditLogs is null) return;
            AutomaticEscalationWorkerOptions options = _options.Value;
            await auditLogs.RecordAsync("automatic-escalation-worker", "System", action, AuditModule.AlertDispatch, "AutomaticEscalationWorker", null, AuditOutcome.Success, reason, null, null, new Dictionary<string, string> { ["processed"] = (response?.Processed ?? 0).ToString(CultureInfo.InvariantCulture), ["escalated"] = (response?.Escalated ?? 0).ToString(CultureInfo.InvariantCulture), ["skipped"] = (response?.Skipped ?? 1).ToString(CultureInfo.InvariantCulture), ["alreadyEscalated"] = (response?.AlreadyEscalated ?? 0).ToString(CultureInfo.InvariantCulture), ["alreadyAcknowledged"] = (response?.AlreadyAcknowledged ?? 0).ToString(CultureInfo.InvariantCulture), ["notReady"] = (response?.NotReady ?? 0).ToString(CultureInfo.InvariantCulture), ["failed"] = (response?.Failed ?? 0).ToString(CultureInfo.InvariantCulture), ["maxItems"] = options.MaxItemsPerRun.ToString(CultureInfo.InvariantCulture), ["escalateAfterSeconds"] = options.EscalateAfterSeconds.ToString(CultureInfo.InvariantCulture), ["runSource"] = "Worker" }, cancellationToken);
        }
        catch
        {
        }
    }
}
