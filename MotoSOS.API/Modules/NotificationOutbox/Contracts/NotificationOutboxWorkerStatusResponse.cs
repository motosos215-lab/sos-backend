namespace MotoSOS.API.Modules.NotificationOutbox.Contracts;

public sealed record NotificationOutboxWorkerStatusResponse(
    bool IsEnabled,
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
