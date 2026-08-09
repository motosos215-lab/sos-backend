namespace MotoSOS.API.Modules.NotificationOutbox.Worker;

public sealed class InMemoryNotificationOutboxWorkerStateStore : INotificationOutboxWorkerStateStore
{
    private readonly object _sync = new();
    private NotificationOutboxWorkerState _state = new(false, null, null, null, 0, 0, 0, 0, null, null);

    public NotificationOutboxWorkerState GetSnapshot()
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

    public void MarkSucceeded(DateTimeOffset completedAtUtc, int processed, int simulatedSent, int failed, int skipped)
    {
        lock (_sync)
        {
            _state = _state with { IsRunning = false, LastRunCompletedAtUtc = completedAtUtc, LastRunSucceeded = true, LastRunProcessed = processed, LastRunSimulatedSent = simulatedSent, LastRunFailed = failed, LastRunSkipped = skipped, LastErrorCode = null, LastErrorMessage = null };
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
            _state = _state with { LastRunCompletedAtUtc = skippedAtUtc, LastRunSucceeded = false, LastRunSkipped = 1, LastErrorCode = "worker_run_overlap", LastErrorMessage = "Previous worker run is still active." };
        }
    }
}
