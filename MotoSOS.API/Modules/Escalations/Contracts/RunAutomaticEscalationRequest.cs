namespace MotoSOS.API.Modules.Escalations.Contracts;

public sealed record RunAutomaticEscalationRequest(int? MaxItems, int? EscalateAfterSeconds);
