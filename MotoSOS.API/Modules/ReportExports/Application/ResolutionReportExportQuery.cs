using MotoSOS.API.Modules.ReportExports.Domain;

namespace MotoSOS.API.Modules.ReportExports.Application;

public sealed record ResolutionReportExportQuery(string? UserId, string? IncidentId, string? EmergencyResolutionReportId, ResolutionReportExportType? ExportType, ResolutionReportExportStatus? Status, DateTimeOffset? DateFrom, DateTimeOffset? DateTo, int PageNumber, int PageSize);
