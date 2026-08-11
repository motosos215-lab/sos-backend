namespace MotoSOS.API.Modules.OfflineProcessing.Worker;

public sealed record OfflineProcessingWorkerState(
    bool IsRunning,
    DateTimeOffset? LastRunStartedAtUtc,
    DateTimeOffset? LastRunCompletedAtUtc,
    bool? LastRunSucceeded,
    int LastProcessedCount,
    int LastFailedCount,
    int LastRecoveredCount,
    string? LastErrorCode,
    string? LastErrorMessage);
