using MotoSOS.API.Modules.ReportExports.Domain;

namespace MotoSOS.API.Modules.ReportExports.Application;

public interface IResolutionReportExportRepository
{
    Task<ResolutionReportExport?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);
    Task<ResolutionReportExport> UpsertAsync(ResolutionReportExport export, CancellationToken cancellationToken);
    Task<IReadOnlyList<ResolutionReportExport>> ListAsync(ResolutionReportExportQuery query, CancellationToken cancellationToken);
    Task<long> CountAsync(ResolutionReportExportQuery query, CancellationToken cancellationToken);
}
