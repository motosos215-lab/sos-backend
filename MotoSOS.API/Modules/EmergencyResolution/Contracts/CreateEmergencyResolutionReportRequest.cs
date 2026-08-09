namespace MotoSOS.API.Modules.EmergencyResolution.Contracts;

public sealed record CreateEmergencyResolutionReportRequest(string? Outcome, string? Summary, string? Notes);
