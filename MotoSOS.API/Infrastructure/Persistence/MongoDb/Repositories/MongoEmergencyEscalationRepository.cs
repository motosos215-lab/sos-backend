using MongoDB.Driver;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Collections;
using MotoSOS.API.Modules.Escalations.Application;
using MotoSOS.API.Modules.Escalations.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class MongoEmergencyEscalationRepository : IEmergencyEscalationRepository
{
    private readonly IMongoCollection<EmergencyEscalation> _escalations;
    public MongoEmergencyEscalationRepository(IMongoDatabase database) => _escalations = database.GetCollection<EmergencyEscalation>(MongoCollectionNames.EmergencyEscalations);
    public async Task<EmergencyEscalation?> GetByIdAsync(string id, CancellationToken cancellationToken) => await _escalations.Find(e => e.Id == id).FirstOrDefaultAsync(cancellationToken);
    public async Task<EmergencyEscalation?> GetByAlertDispatchIdAsync(string alertDispatchId, CancellationToken cancellationToken) => await _escalations.Find(e => e.AlertDispatchId == alertDispatchId).FirstOrDefaultAsync(cancellationToken);
    public async Task<EmergencyEscalation?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) => await _escalations.Find(e => e.IdempotencyKey == idempotencyKey).FirstOrDefaultAsync(cancellationToken);
    public async Task<(EmergencyEscalation Escalation, bool IsDuplicate)> AddOrGetDuplicateAsync(EmergencyEscalation escalation, CancellationToken cancellationToken)
    {
        EmergencyEscalation? existing = await GetByIdempotencyKeyAsync(escalation.IdempotencyKey, cancellationToken) ?? await GetByAlertDispatchIdAsync(escalation.AlertDispatchId, cancellationToken);
        if (existing is not null) return (existing, true);
        try { await _escalations.InsertOneAsync(escalation, cancellationToken: cancellationToken); return (escalation, false); }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            existing = await GetByIdempotencyKeyAsync(escalation.IdempotencyKey, cancellationToken) ?? await GetByAlertDispatchIdAsync(escalation.AlertDispatchId, cancellationToken);
            if (existing is not null) return (existing, true);
            throw;
        }
    }
    public async Task<IReadOnlyList<EmergencyEscalation>> ListAsync(EmergencyEscalationQuery query, CancellationToken cancellationToken) => await _escalations.Find(BuildFilter(query)).SortByDescending(e => e.CreatedAtUtc).Skip((query.PageNumber - 1) * query.PageSize).Limit(query.PageSize).ToListAsync(cancellationToken);
    public async Task<long> CountAsync(EmergencyEscalationQuery query, CancellationToken cancellationToken) => await _escalations.CountDocumentsAsync(BuildFilter(query), cancellationToken: cancellationToken);
    public async Task UpdateAsync(EmergencyEscalation escalation, CancellationToken cancellationToken) => await _escalations.ReplaceOneAsync(e => e.Id == escalation.Id, escalation, cancellationToken: cancellationToken);
    private static FilterDefinition<EmergencyEscalation> BuildFilter(EmergencyEscalationQuery q) { var b = Builders<EmergencyEscalation>.Filter; var f = b.Empty; if (q.Status.HasValue) f &= b.Eq(e => e.Status, q.Status.Value); if (q.Reason.HasValue) f &= b.Eq(e => e.Reason, q.Reason.Value); if (q.Level.HasValue) f &= b.Eq(e => e.Level, q.Level.Value); if (q.DateFrom.HasValue) f &= b.Gte(e => e.CreatedAtUtc, q.DateFrom.Value); if (q.DateTo.HasValue) f &= b.Lte(e => e.CreatedAtUtc, q.DateTo.Value); return f; }
}
