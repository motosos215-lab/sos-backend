namespace MotoSOS.API.Modules.EmergencyStatus.Contracts;

public sealed record GetActiveEmergenciesResponse(IReadOnlyList<EmergencyStatusResponse> Emergencies, int PageNumber, int PageSize, long TotalCount);
