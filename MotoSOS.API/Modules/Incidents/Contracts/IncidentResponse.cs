namespace MotoSOS.API.Modules.Incidents.Contracts;

public sealed record IncidentResponse(string Id, string TripId, string VehicleId, string MobileDeviceId, string? SmartwatchDeviceId, string Source, string Cause, string RiskLevel, string Status, int? Score, double? Confidence, DateTimeOffset OccurredAtUtc, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc, DateTimeOffset? CancelledAtUtc, DateTimeOffset? ClosedAtUtc, string? ClosureReason, string? ClosureNotes);
