namespace MotoSOS.API.Modules.Escalations.Contracts;

public sealed record GetEmergencyEscalationsResponse(IReadOnlyList<EmergencyEscalationResponse> Escalations, int PageNumber, int PageSize, long TotalCount);
