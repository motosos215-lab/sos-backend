namespace MotoSOS.API.Modules.ReportExports.Contracts;

public sealed record ResolutionReportIncidentSummaryResponse(string IncidentId, string IncidentStatus, string IncidentCause, string RiskLevel, DateTimeOffset OccurredAtUtc, DateTimeOffset? ClosedAtUtc, DateTimeOffset? CancelledAtUtc, string? ClosureReason, string? ClosureNotes);
