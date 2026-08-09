using MotoSOS.API.Modules.ReportExports.Application;
using MotoSOS.API.Modules.ReportExports.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class UnconfiguredResolutionReportExportRepository : IResolutionReportExportRepository
{
    private static InvalidOperationException CreateException() => new("MongoDB is not configured. Configure MongoDB settings to use Resolution Report Export API.");
    public Task<ResolutionReportExport?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) => throw CreateException();
    public Task<ResolutionReportExport> UpsertAsync(ResolutionReportExport export, CancellationToken cancellationToken) => throw CreateException();
    public Task<IReadOnlyList<ResolutionReportExport>> ListAsync(ResolutionReportExportQuery query, CancellationToken cancellationToken) => throw CreateException();
    public Task<long> CountAsync(ResolutionReportExportQuery query, CancellationToken cancellationToken) => throw CreateException();
}
