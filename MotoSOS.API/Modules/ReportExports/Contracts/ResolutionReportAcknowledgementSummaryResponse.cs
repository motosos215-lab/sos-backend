namespace MotoSOS.API.Modules.ReportExports.Contracts;

public sealed record ResolutionReportAcknowledgementSummaryResponse(int AcknowledgementsTotal, int AcknowledgedCount, int DeclinedCount, int ViewedCount, int PendingCount, DateTimeOffset? FirstAcknowledgedAtUtc);
