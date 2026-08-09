namespace MotoSOS.API.Modules.ReportExports.Contracts;

public sealed record ResolutionReportExportMetadataResponse(string Id, string UserId, string IncidentId, string EmergencyResolutionReportId, string ExportType, string Status, string RequestedByUserId, string RequestedByRole, DateTimeOffset GeneratedAtUtc, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);

public sealed record GetResolutionReportExportsResponse(IReadOnlyList<ResolutionReportExportMetadataResponse> Exports, int PageNumber, int PageSize, long TotalCount);
