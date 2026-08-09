namespace MotoSOS.API.Modules.OperationalDashboard.Contracts;

public sealed record OperationalDashboardIncidentListItemResponse(string IncidentId, string TripId, string Status, string Source, string Cause, string RiskLevel, DateTimeOffset OccurredAtUtc, DateTimeOffset CreatedAtUtc, DateTimeOffset? ClosedAtUtc);
