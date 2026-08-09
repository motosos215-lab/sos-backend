namespace MotoSOS.API.Modules.Incidents.Contracts;

public sealed record CreateIncidentRequest(string? TripId, string? ClientIncidentId, string? Source, string? Cause, string? RiskLevel, int? Score, double? Confidence, string? GpsQuality, string? RuleSetVersion, string? ValidationPolicyVersion, DateTimeOffset? OccurredAtUtc, IncidentLocationRequest? Location, IncidentEvidenceSummaryRequest? EvidenceSummary);
