namespace MotoSOS.API.Modules.Escalations.Contracts;

public sealed record RunAutomaticEscalationResponse(
    int Processed,
    int Escalated,
    int Skipped,
    int AlreadyEscalated,
    int AlreadyAcknowledged,
    int NotReady,
    int Failed);
