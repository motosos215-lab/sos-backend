using MongoDB.Driver;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Collections;
using MotoSOS.API.Modules.AlertAcknowledgements.Application;
using MotoSOS.API.Modules.AlertAcknowledgements.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class MongoAlertAcknowledgementRepository : IAlertAcknowledgementRepository
{
    private readonly IMongoCollection<AlertAcknowledgement> _acks;
    public MongoAlertAcknowledgementRepository(IMongoDatabase database) => _acks = database.GetCollection<AlertAcknowledgement>(MongoCollectionNames.AlertAcknowledgements);
    public async Task<AlertAcknowledgement?> GetByIdAsync(string id, CancellationToken cancellationToken) => await _acks.Find(a => a.Id == id).FirstOrDefaultAsync(cancellationToken);
    public async Task<AlertAcknowledgement?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) => await _acks.Find(a => a.IdempotencyKey == idempotencyKey).FirstOrDefaultAsync(cancellationToken);
    public async Task<(AlertAcknowledgement Acknowledgement, bool IsDuplicate)> AddOrGetDuplicateAsync(AlertAcknowledgement acknowledgement, CancellationToken cancellationToken)
    {
        AlertAcknowledgement? existing = await GetByIdempotencyKeyAsync(acknowledgement.IdempotencyKey, cancellationToken);
        if (existing is not null) return (existing, true);
        try { await _acks.InsertOneAsync(acknowledgement, cancellationToken: cancellationToken); return (acknowledgement, false); }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            existing = await GetByIdempotencyKeyAsync(acknowledgement.IdempotencyKey, cancellationToken);
            if (existing is not null) return (existing, true);
            throw;
        }
    }
    public async Task<IReadOnlyList<AlertAcknowledgement>> ListByIncidentIdAsync(string userId, string incidentId, CancellationToken cancellationToken) => await _acks.Find(a => a.UserId == userId && a.IncidentId == incidentId).SortByDescending(a => a.CreatedAtUtc).ToListAsync(cancellationToken);
    public async Task<IReadOnlyList<AlertAcknowledgement>> ListByAlertDispatchIdAsync(string userId, string alertDispatchId, CancellationToken cancellationToken) => await _acks.Find(a => a.UserId == userId && a.AlertDispatchId == alertDispatchId).SortByDescending(a => a.CreatedAtUtc).ToListAsync(cancellationToken);
    public async Task<IReadOnlyList<AlertAcknowledgement>> ListByMonitorUserIdAsync(string monitorUserId, AlertAcknowledgementStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken) => await _acks.Find(BuildMonitorFilter(monitorUserId, status)).SortByDescending(a => a.CreatedAtUtc).Skip((pageNumber - 1) * pageSize).Limit(pageSize).ToListAsync(cancellationToken);
    public async Task<long> CountByMonitorUserIdAsync(string monitorUserId, AlertAcknowledgementStatus? status, CancellationToken cancellationToken) => await _acks.CountDocumentsAsync(BuildMonitorFilter(monitorUserId, status), cancellationToken: cancellationToken);
    public async Task<IReadOnlyList<AlertAcknowledgement>> ListByUserIdAsync(string userId, string? alertDispatchId, string? incidentId, AlertAcknowledgementStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken) => await _acks.Find(BuildUserFilter(userId, alertDispatchId, incidentId, status)).SortByDescending(a => a.CreatedAtUtc).Skip((pageNumber - 1) * pageSize).Limit(pageSize).ToListAsync(cancellationToken);
    public async Task<long> CountByUserIdAsync(string userId, string? alertDispatchId, string? incidentId, AlertAcknowledgementStatus? status, CancellationToken cancellationToken) => await _acks.CountDocumentsAsync(BuildUserFilter(userId, alertDispatchId, incidentId, status), cancellationToken: cancellationToken);
    public async Task UpdateAsync(AlertAcknowledgement acknowledgement, CancellationToken cancellationToken) => await _acks.ReplaceOneAsync(a => a.Id == acknowledgement.Id, acknowledgement, cancellationToken: cancellationToken);
    private static FilterDefinition<AlertAcknowledgement> BuildMonitorFilter(string monitorUserId, AlertAcknowledgementStatus? status) { var b = Builders<AlertAcknowledgement>.Filter; var f = b.Eq(a => a.MonitorUserId, monitorUserId); if (status.HasValue) f &= b.Eq(a => a.Status, status.Value); return f; }
    private static FilterDefinition<AlertAcknowledgement> BuildUserFilter(string userId, string? alertDispatchId, string? incidentId, AlertAcknowledgementStatus? status) { var b = Builders<AlertAcknowledgement>.Filter; var f = b.Eq(a => a.UserId, userId); if (!string.IsNullOrWhiteSpace(alertDispatchId)) f &= b.Eq(a => a.AlertDispatchId, alertDispatchId); if (!string.IsNullOrWhiteSpace(incidentId)) f &= b.Eq(a => a.IncidentId, incidentId); if (status.HasValue) f &= b.Eq(a => a.Status, status.Value); return f; }
}
