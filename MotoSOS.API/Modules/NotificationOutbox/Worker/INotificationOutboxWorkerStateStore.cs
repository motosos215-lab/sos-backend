namespace MotoSOS.API.Modules.NotificationOutbox.Worker;

public interface INotificationOutboxWorkerStateStore
{
    NotificationOutboxWorkerState GetSnapshot();
    void MarkStarted(DateTimeOffset startedAtUtc);
    void MarkSucceeded(DateTimeOffset completedAtUtc, int processed, int simulatedSent, int failed, int skipped);
    void MarkFailed(DateTimeOffset completedAtUtc, string errorCode, string errorMessage);
    void MarkSkipped(DateTimeOffset skippedAtUtc);
}
