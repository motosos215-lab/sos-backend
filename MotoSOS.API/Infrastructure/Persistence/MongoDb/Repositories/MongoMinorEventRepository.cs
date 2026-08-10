using MongoDB.Driver;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Collections;
using MotoSOS.API.Modules.MinorEvents.Application;
using MotoSOS.API.Modules.MinorEvents.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class MongoMinorEventRepository : IMinorEventRepository
{
    private readonly IMongoCollection<MinorEvent> _minorEvents;
    public MongoMinorEventRepository(IMongoDatabase database) => _minorEvents = database.GetCollection<MinorEvent>(MongoCollectionNames.MinorEvents);
    public async Task<MinorEvent?> GetByIdAsync(string id, CancellationToken cancellationToken) => await _minorEvents.Find(e => e.Id == id).FirstOrDefaultAsync(cancellationToken);
    public async Task<MinorEvent?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) => await _minorEvents.Find(e => e.IdempotencyKey == idempotencyKey).FirstOrDefaultAsync(cancellationToken);
    public async Task<(MinorEvent MinorEvent, bool IsDuplicate)> AddOrGetDuplicateAsync(MinorEvent minorEvent, CancellationToken cancellationToken)
    {
        MinorEvent? existing = await GetByIdempotencyKeyAsync(minorEvent.IdempotencyKey, cancellationToken);
        if (existing is not null) return (existing, true);
        try { await _minorEvents.InsertOneAsync(minorEvent, cancellationToken: cancellationToken); return (minorEvent, false); }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            existing = await GetByIdempotencyKeyAsync(minorEvent.IdempotencyKey, cancellationToken);
            if (existing is not null) return (existing, true);
            throw;
        }
    }
    public async Task UpdateAsync(MinorEvent minorEvent, CancellationToken cancellationToken) => await _minorEvents.ReplaceOneAsync(e => e.Id == minorEvent.Id, minorEvent, cancellationToken: cancellationToken);
    public async Task<IReadOnlyList<MinorEvent>> ListByUserIdAsync(string userId, MinorEventQuery query, CancellationToken cancellationToken) => await _minorEvents.Find(BuildFilter(query) & Builders<MinorEvent>.Filter.Eq(e => e.UserId, userId)).SortByDescending(e => e.OccurredAtUtc).Skip((query.PageNumber - 1) * query.PageSize).Limit(query.PageSize).ToListAsync(cancellationToken);
    public async Task<IReadOnlyList<MinorEvent>> ListByTripIdAsync(string userId, string tripId, CancellationToken cancellationToken) => await _minorEvents.Find(e => e.UserId == userId && e.TripId == tripId).SortBy(e => e.OccurredAtUtc).ToListAsync(cancellationToken);
    public async Task<long> CountByUserIdAsync(string userId, MinorEventQuery query, CancellationToken cancellationToken) => await _minorEvents.CountDocumentsAsync(BuildFilter(query) & Builders<MinorEvent>.Filter.Eq(e => e.UserId, userId), cancellationToken: cancellationToken);
    public async Task<IReadOnlyList<MinorEvent>> ListAsync(MinorEventQuery query, CancellationToken cancellationToken) => await _minorEvents.Find(BuildFilter(query)).SortByDescending(e => e.OccurredAtUtc).Skip((query.PageNumber - 1) * query.PageSize).Limit(query.PageSize).ToListAsync(cancellationToken);
    public async Task<long> CountAsync(MinorEventQuery query, CancellationToken cancellationToken) => await _minorEvents.CountDocumentsAsync(BuildFilter(query), cancellationToken: cancellationToken);
    private static FilterDefinition<MinorEvent> BuildFilter(MinorEventQuery q) { var b = Builders<MinorEvent>.Filter; var f = b.Empty; if (!string.IsNullOrWhiteSpace(q.UserId)) f &= b.Eq(e => e.UserId, q.UserId); if (!string.IsNullOrWhiteSpace(q.TripId)) f &= b.Eq(e => e.TripId, q.TripId); if (q.EventType.HasValue) f &= b.Eq(e => e.EventType, q.EventType.Value); if (q.Severity.HasValue) f &= b.Eq(e => e.Severity, q.Severity.Value); if (q.Status.HasValue) f &= b.Eq(e => e.Status, q.Status.Value); if (q.DateFrom.HasValue) f &= b.Gte(e => e.OccurredAtUtc, q.DateFrom.Value); if (q.DateTo.HasValue) f &= b.Lte(e => e.OccurredAtUtc, q.DateTo.Value); return f; }
}
