namespace MotoSOS.API.Modules.OfflineProcessing.Worker;

public sealed class InMemoryOfflineProcessingWorkerStateStore : IOfflineProcessingWorkerStateStore
{
    private readonly object _sync = new();
    private OfflineProcessingWorkerState _state = new(false, null, null, null, 0, 0, 0, null, null);

    public OfflineProcessingWorkerState GetSnapshot()
    {
        lock (_sync)
        {
            return _state;
        }
    }

    public void MarkStarted(DateTimeOffset startedAtUtc)
    {
        lock (_sync)
        {
            _state = _state with { IsRunning = true, LastRunStartedAtUtc = startedAtUtc, LastRunSucceeded = null, LastErrorCode = null, LastErrorMessage = null };
        }
    }

    public void MarkSucceeded(DateTimeOffset completedAtUtc, int processed, int failed, int recovered)
    {
        lock (_sync)
        {
            _state = _state with { IsRunning = false, LastRunCompletedAtUtc = completedAtUtc, LastRunSucceeded = true, LastProcessedCount = processed, LastFailedCount = failed, LastRecoveredCount = recovered, LastErrorCode = null, LastErrorMessage = null };
        }
    }

    public void MarkFailed(DateTimeOffset completedAtUtc, string errorCode, string errorMessage)
    {
        lock (_sync)
        {
            _state = _state with { IsRunning = false, LastRunCompletedAtUtc = completedAtUtc, LastRunSucceeded = false, LastErrorCode = errorCode, LastErrorMessage = errorMessage };
        }
    }

    public void MarkSkipped(DateTimeOffset skippedAtUtc)
    {
        lock (_sync)
        {
            _state = _state with { LastRunCompletedAtUtc = skippedAtUtc, LastRunSucceeded = false, LastErrorCode = "offline_processing_worker_overlap", LastErrorMessage = "Previous worker run is still active." };
        }
    }
}
