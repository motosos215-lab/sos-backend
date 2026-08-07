namespace MotoSOS.API.Modules.EmergencyStatus.Contracts;

public sealed record EmergencyIncidentStatusResponse(string Id, string Status, string Source, string Cause, string RiskLevel, DateTimeOffset OccurredAtUtc, DateTimeOffset CreatedAtUtc);
