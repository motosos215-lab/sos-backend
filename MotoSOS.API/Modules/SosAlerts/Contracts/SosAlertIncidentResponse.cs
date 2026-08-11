namespace MotoSOS.API.Modules.SosAlerts.Contracts;

public sealed record SosAlertIncidentResponse(string Id, string TripId, string Status, string IncidentType, string Severity);
