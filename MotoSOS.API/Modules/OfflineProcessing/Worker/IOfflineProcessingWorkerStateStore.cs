namespace MotoSOS.API.Modules.OfflineProcessing.Worker;

public interface IOfflineProcessingWorkerStateStore
{
    OfflineProcessingWorkerState GetSnapshot();
    void MarkStarted(DateTimeOffset startedAtUtc);
    void MarkSucceeded(DateTimeOffset completedAtUtc, int processed, int failed, int recovered);
    void MarkFailed(DateTimeOffset completedAtUtc, string errorCode, string errorMessage);
    void MarkSkipped(DateTimeOffset skippedAtUtc);
}
