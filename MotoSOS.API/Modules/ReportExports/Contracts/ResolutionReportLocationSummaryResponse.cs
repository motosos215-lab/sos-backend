namespace MotoSOS.API.Modules.ReportExports.Contracts;

public sealed record ResolutionReportLocationSummaryResponse(double? LastKnownLatitude, double? LastKnownLongitude, DateTimeOffset? RecordedAtUtc, bool? IsStale);
