namespace MotoSOS.API.Modules.ReportExports.Contracts;

public sealed record ResolutionReportResolutionSummaryResponse(string Outcome, string? Notes, DateTimeOffset CreatedAtUtc, long? ResponseTimeSeconds, bool? FinalLocationWasStale, string ClosedByRole);
