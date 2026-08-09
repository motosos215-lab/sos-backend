namespace MotoSOS.API.Modules.ReportExports.Contracts;

public sealed record ResolutionReportAuditSummaryResponse(long TotalAuditEvents, string? LastRelevantAuditAction, DateTimeOffset? LastRelevantAuditAtUtc);
