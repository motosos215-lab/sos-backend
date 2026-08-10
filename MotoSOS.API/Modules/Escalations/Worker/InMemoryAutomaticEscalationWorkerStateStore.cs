namespace MotoSOS.API.Modules.Escalations.Worker;

public sealed class InMemoryAutomaticEscalationWorkerStateStore : IAutomaticEscalationWorkerStateStore
{
    private readonly object _sync = new();
    private AutomaticEscalationWorkerState _state = new(false, null, null, null, 0, 0, 0, 0, 0, 0, 0, null, null);

    public AutomaticEscalationWorkerState GetSnapshot()
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

    public void MarkSucceeded(DateTimeOffset completedAtUtc, int processed, int escalated, int skipped, int alreadyEscalated, int alreadyAcknowledged, int notReady, int failed)
    {
        lock (_sync)
        {
            _state = _state with { IsRunning = false, LastRunCompletedAtUtc = completedAtUtc, LastRunSucceeded = true, LastRunProcessed = processed, LastRunEscalated = escalated, LastRunSkipped = skipped, LastRunAlreadyEscalated = alreadyEscalated, LastRunAlreadyAcknowledged = alreadyAcknowledged, LastRunNotReady = notReady, LastRunFailed = failed, LastErrorCode = null, LastErrorMessage = null };
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
            _state = _state with { LastRunCompletedAtUtc = skippedAtUtc, LastRunSucceeded = false, LastRunSkipped = 1, LastErrorCode = "automatic_escalation_worker_overlap", LastErrorMessage = "Previous automatic escalation worker run is still active." };
        }
    }
}
