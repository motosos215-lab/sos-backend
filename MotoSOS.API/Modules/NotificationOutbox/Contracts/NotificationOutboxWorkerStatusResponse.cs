namespace MotoSOS.API.Modules.NotificationOutbox.Contracts;

public sealed record NotificationOutboxWorkerStatusResponse(
    bool Enabled,
    bool Running,
    int IntervalSeconds,
    int MaxItemsPerRun,
    bool SimulateFailures,
    bool RunOnStartup,
    DateTimeOffset? LastRunStartedAtUtc,
    DateTimeOffset? LastRunCompletedAtUtc,
    int LastProcessedCount,
    int LastFailedCount,
    string? LastError);
