using MotoSOS.API.Modules.ReportExports.Contracts;
using MotoSOS.API.Modules.ReportExports.Domain;

namespace MotoSOS.API.Modules.ReportExports.Application;

public interface IResolutionReportExportService
{
    Task<ResolutionReportExportResponse> ExportForRiderAsync(string userId, string incidentId, ResolutionReportExportType exportType, CancellationToken cancellationToken);
    Task<ResolutionReportExportResponse> ExportForAdminAsync(string userId, string incidentId, ResolutionReportExportType exportType, CancellationToken cancellationToken);
    Task<ResolutionReportExportResponse> ExportForMonitorAsync(string userId, string notificationDeliveryAttemptId, ResolutionReportExportType exportType, CancellationToken cancellationToken);
    Task<GetResolutionReportExportsResponse> ListForAdminAsync(string userId, ResolutionReportExportQuery query, CancellationToken cancellationToken);
}
