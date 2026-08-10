namespace MotoSOS.API.Modules.Escalations.Worker;

public interface IAutomaticEscalationWorkerStateStore
{
    AutomaticEscalationWorkerState GetSnapshot();
    void MarkStarted(DateTimeOffset startedAtUtc);
    void MarkSucceeded(DateTimeOffset completedAtUtc, int processed, int escalated, int skipped, int alreadyEscalated, int alreadyAcknowledged, int notReady, int failed);
    void MarkFailed(DateTimeOffset completedAtUtc, string errorCode, string errorMessage);
    void MarkSkipped(DateTimeOffset skippedAtUtc);
}
