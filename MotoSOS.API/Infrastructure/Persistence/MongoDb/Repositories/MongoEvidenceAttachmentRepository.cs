using MongoDB.Driver;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Collections;
using MotoSOS.API.Modules.EvidenceAttachments.Application;
using MotoSOS.API.Modules.EvidenceAttachments.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class MongoEvidenceAttachmentRepository : IEvidenceAttachmentRepository
{
    private readonly IMongoCollection<EvidenceAttachment> _evidence;
    public MongoEvidenceAttachmentRepository(IMongoDatabase database) => _evidence = database.GetCollection<EvidenceAttachment>(MongoCollectionNames.EvidenceAttachments);
    public async Task<EvidenceAttachment?> GetByIdAsync(string id, CancellationToken cancellationToken) => await _evidence.Find(e => e.Id == id).FirstOrDefaultAsync(cancellationToken);
    public async Task<EvidenceAttachment?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) => await _evidence.Find(e => e.IdempotencyKey == idempotencyKey).FirstOrDefaultAsync(cancellationToken);
    public async Task<EvidenceAttachment?> GetByClientEvidenceIdAsync(string userId, string incidentId, string clientEvidenceId, CancellationToken cancellationToken) => await _evidence.Find(e => e.UserId == userId && e.IncidentId == incidentId && e.ClientEvidenceId == clientEvidenceId).FirstOrDefaultAsync(cancellationToken);
    public async Task<(EvidenceAttachment EvidenceAttachment, bool IsDuplicate)> AddOrGetDuplicateAsync(EvidenceAttachment evidenceAttachment, CancellationToken cancellationToken)
    {
        EvidenceAttachment? existing = await GetByIdempotencyKeyAsync(evidenceAttachment.IdempotencyKey, cancellationToken);
        if (existing is not null) return (existing, true);
        try { await _evidence.InsertOneAsync(evidenceAttachment, cancellationToken: cancellationToken); return (evidenceAttachment, false); }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            existing = await GetByIdempotencyKeyAsync(evidenceAttachment.IdempotencyKey, cancellationToken);
            if (existing is not null) return (existing, true);
            throw;
        }
    }
    public async Task UpdateAsync(EvidenceAttachment evidenceAttachment, CancellationToken cancellationToken) => await _evidence.ReplaceOneAsync(e => e.Id == evidenceAttachment.Id, evidenceAttachment, cancellationToken: cancellationToken);
    public async Task<IReadOnlyList<EvidenceAttachment>> ListByUserIdAsync(string userId, EvidenceAttachmentQuery query, CancellationToken cancellationToken) => await _evidence.Find(BuildFilter(query) & Builders<EvidenceAttachment>.Filter.Eq(e => e.UserId, userId)).SortByDescending(e => e.CreatedAtUtc).Skip((query.PageNumber - 1) * query.PageSize).Limit(query.PageSize).ToListAsync(cancellationToken);
    public async Task<long> CountByUserIdAsync(string userId, EvidenceAttachmentQuery query, CancellationToken cancellationToken) => await _evidence.CountDocumentsAsync(BuildFilter(query) & Builders<EvidenceAttachment>.Filter.Eq(e => e.UserId, userId), cancellationToken: cancellationToken);
    public async Task<IReadOnlyList<EvidenceAttachment>> ListAsync(EvidenceAttachmentQuery query, CancellationToken cancellationToken) => await _evidence.Find(BuildFilter(query)).SortByDescending(e => e.CreatedAtUtc).Skip((query.PageNumber - 1) * query.PageSize).Limit(query.PageSize).ToListAsync(cancellationToken);
    public async Task<long> CountAsync(EvidenceAttachmentQuery query, CancellationToken cancellationToken) => await _evidence.CountDocumentsAsync(BuildFilter(query), cancellationToken: cancellationToken);
    private static FilterDefinition<EvidenceAttachment> BuildFilter(EvidenceAttachmentQuery q) { var b = Builders<EvidenceAttachment>.Filter; var f = b.Empty; if (!string.IsNullOrWhiteSpace(q.UserId)) f &= b.Eq(e => e.UserId, q.UserId); if (!string.IsNullOrWhiteSpace(q.IncidentId)) f &= b.Eq(e => e.IncidentId, q.IncidentId); if (!string.IsNullOrWhiteSpace(q.AlertDispatchId)) f &= b.Eq(e => e.AlertDispatchId, q.AlertDispatchId); if (!string.IsNullOrWhiteSpace(q.EmergencyResolutionReportId)) f &= b.Eq(e => e.EmergencyResolutionReportId, q.EmergencyResolutionReportId); if (q.EvidenceType.HasValue) f &= b.Eq(e => e.EvidenceType, q.EvidenceType.Value); if (q.Source.HasValue) f &= b.Eq(e => e.Source, q.Source.Value); if (q.Status.HasValue) f &= b.Eq(e => e.Status, q.Status.Value); if (q.DateFrom.HasValue) f &= b.Gte(e => e.CreatedAtUtc, q.DateFrom.Value); if (q.DateTo.HasValue) f &= b.Lte(e => e.CreatedAtUtc, q.DateTo.Value); return f; }
}
