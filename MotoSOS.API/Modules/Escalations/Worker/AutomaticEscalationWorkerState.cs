namespace MotoSOS.API.Modules.Escalations.Worker;

public sealed record AutomaticEscalationWorkerState(
    bool IsRunning,
    DateTimeOffset? LastRunStartedAtUtc,
    DateTimeOffset? LastRunCompletedAtUtc,
    bool? LastRunSucceeded,
    int LastRunProcessed,
    int LastRunEscalated,
    int LastRunSkipped,
    int LastRunAlreadyEscalated,
    int LastRunAlreadyAcknowledged,
    int LastRunNotReady,
    int LastRunFailed,
    string? LastErrorCode,
    string? LastErrorMessage);
