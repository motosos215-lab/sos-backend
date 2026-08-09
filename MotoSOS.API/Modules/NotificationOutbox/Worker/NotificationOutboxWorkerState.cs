namespace MotoSOS.API.Modules.NotificationOutbox.Worker;

public sealed record NotificationOutboxWorkerState(
    bool IsRunning,
    DateTimeOffset? LastRunStartedAtUtc,
    DateTimeOffset? LastRunCompletedAtUtc,
    bool? LastRunSucceeded,
    int LastRunProcessed,
    int LastRunSimulatedSent,
    int LastRunFailed,
    int LastRunSkipped,
    string? LastErrorCode,
    string? LastErrorMessage);
