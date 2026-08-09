using MongoDB.Driver;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Collections;
using MotoSOS.API.Modules.ReportExports.Application;
using MotoSOS.API.Modules.ReportExports.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class MongoResolutionReportExportRepository : IResolutionReportExportRepository
{
    private readonly IMongoCollection<ResolutionReportExport> _exports;
    public MongoResolutionReportExportRepository(IMongoDatabase database) => _exports = database.GetCollection<ResolutionReportExport>(MongoCollectionNames.ResolutionReportExports);
    public async Task<ResolutionReportExport?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) => await _exports.Find(e => e.IdempotencyKey == idempotencyKey).FirstOrDefaultAsync(cancellationToken);
    public async Task<ResolutionReportExport> UpsertAsync(ResolutionReportExport export, CancellationToken cancellationToken) { await _exports.ReplaceOneAsync(e => e.IdempotencyKey == export.IdempotencyKey, export, new ReplaceOptions { IsUpsert = true }, cancellationToken); return await GetByIdempotencyKeyAsync(export.IdempotencyKey, cancellationToken) ?? export; }
    public async Task<IReadOnlyList<ResolutionReportExport>> ListAsync(ResolutionReportExportQuery query, CancellationToken cancellationToken) => await _exports.Find(BuildFilter(query)).SortByDescending(e => e.GeneratedAtUtc).Skip((query.PageNumber - 1) * query.PageSize).Limit(query.PageSize).ToListAsync(cancellationToken);
    public async Task<long> CountAsync(ResolutionReportExportQuery query, CancellationToken cancellationToken) => await _exports.CountDocumentsAsync(BuildFilter(query), cancellationToken: cancellationToken);
    private static FilterDefinition<ResolutionReportExport> BuildFilter(ResolutionReportExportQuery q) { var b = Builders<ResolutionReportExport>.Filter; var f = b.Empty; if (!string.IsNullOrWhiteSpace(q.UserId)) f &= b.Eq(e => e.UserId, q.UserId); if (!string.IsNullOrWhiteSpace(q.IncidentId)) f &= b.Eq(e => e.IncidentId, q.IncidentId); if (!string.IsNullOrWhiteSpace(q.EmergencyResolutionReportId)) f &= b.Eq(e => e.EmergencyResolutionReportId, q.EmergencyResolutionReportId); if (q.ExportType.HasValue) f &= b.Eq(e => e.ExportType, q.ExportType.Value); if (q.Status.HasValue) f &= b.Eq(e => e.Status, q.Status.Value); if (q.DateFrom.HasValue) f &= b.Gte(e => e.GeneratedAtUtc, q.DateFrom.Value); if (q.DateTo.HasValue) f &= b.Lte(e => e.GeneratedAtUtc, q.DateTo.Value); return f; }
}
