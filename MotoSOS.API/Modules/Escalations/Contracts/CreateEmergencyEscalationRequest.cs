namespace MotoSOS.API.Modules.Escalations.Contracts;

public sealed record CreateEmergencyEscalationRequest(string? Reason, string? Level, string? Notes);
