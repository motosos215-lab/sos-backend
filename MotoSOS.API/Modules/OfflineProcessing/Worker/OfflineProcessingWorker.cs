using Microsoft.Extensions.Options;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Modules.OfflineProcessing.Application;
using MotoSOS.API.Modules.OfflineProcessing.Contracts;

namespace MotoSOS.API.Modules.OfflineProcessing.Worker;

public sealed class OfflineProcessingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<OfflineProcessingWorkerOptions> _options;
    private readonly IOfflineProcessingWorkerStateStore _state;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _runLock = new(1, 1);

    public OfflineProcessingWorker(IServiceScopeFactory scopeFactory, IOptions<OfflineProcessingWorkerOptions> options, IOfflineProcessingWorkerStateStore state, IClock clock)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _state = state;
        _clock = clock;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        OfflineProcessingWorkerOptions options = _options.Value;
        if (!options.Enabled) return;

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
        OfflineProcessingWorkerOptions options = _options.Value;
        if (!options.Enabled) return;

        bool lockTaken = false;
        try
        {
            if (!await _runLock.WaitAsync(0, cancellationToken))
            {
                _state.MarkSkipped(_clock.UtcNow);
                return;
            }

            lockTaken = true;
            _state.MarkStarted(_clock.UtcNow);
            using IServiceScope scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IOfflineProcessingService>();
            RunOfflineProcessingResponse response = await service.RunWorkerAsync(options.MaxItemsPerRun, options.RecoveryMinutes, cancellationToken);
            _state.MarkSucceeded(_clock.UtcNow, response.Processed, response.Failed, response.Recovered);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _state.MarkFailed(_clock.UtcNow, "offline_processing_worker_cancelled", "Worker execution was cancelled.");
        }
        catch
        {
            _state.MarkFailed(_clock.UtcNow, "offline_processing_worker_failed", "Worker execution failed in a controlled way.");
        }
        finally
        {
            if (lockTaken) _runLock.Release();
        }
    }
}
