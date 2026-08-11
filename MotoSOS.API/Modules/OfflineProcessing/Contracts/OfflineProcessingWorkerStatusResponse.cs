namespace MotoSOS.API.Modules.OfflineProcessing.Contracts;

public sealed record OfflineProcessingWorkerStatusResponse(
    bool WorkerEnabled,
    bool WorkerRunning,
    int IntervalSeconds,
    int MaxItemsPerRun,
    bool RunOnStartup,
    int RecoveryMinutes,
    long PendingCount,
    long ProcessingCount,
    long ProcessedCount,
    long FailedCount,
    DateTimeOffset? LastRunStartedAtUtc,
    DateTimeOffset? LastRunCompletedAtUtc,
    int LastProcessedCount,
    int LastFailedCount,
    int LastRecoveredCount,
    string? LastError);
