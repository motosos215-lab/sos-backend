namespace MotoSOS.API.Modules.ReportExports.Contracts;

public sealed record ResolutionReportExportHeaderResponse(string ReportTitle, DateTimeOffset GeneratedAtUtc, string IncidentId, string ResolutionReportId, string ExportType, string GeneratedByRole);
