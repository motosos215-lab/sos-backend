namespace MotoSOS.API.Modules.EmergencyResolution.Contracts;

public sealed record GetEmergencyResolutionReportsResponse(IReadOnlyList<EmergencyResolutionReportResponse> Reports, int PageNumber, int PageSize, long TotalCount);
