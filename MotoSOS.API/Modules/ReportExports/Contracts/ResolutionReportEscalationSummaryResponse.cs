namespace MotoSOS.API.Modules.ReportExports.Contracts;

public sealed record ResolutionReportEscalationSummaryResponse(string? EscalationStatus, string? Reason, string? Level, DateTimeOffset? CreatedAtUtc, DateTimeOffset? ResolvedAtUtc);
