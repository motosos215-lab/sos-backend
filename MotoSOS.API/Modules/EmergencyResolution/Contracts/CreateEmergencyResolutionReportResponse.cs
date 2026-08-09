namespace MotoSOS.API.Modules.EmergencyResolution.Contracts;

public sealed record CreateEmergencyResolutionReportResponse(EmergencyResolutionReportResponse Report, bool IsDuplicate);
