using MongoDB.Driver;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Collections;
using MotoSOS.API.Modules.TelemetrySummary.Application;
using MotoSOS.API.Modules.TelemetrySummary.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class MongoTelemetrySummaryRepository : ITelemetrySummaryRepository
{
    private readonly IMongoCollection<TripTelemetrySummary> _summaries;
    public MongoTelemetrySummaryRepository(IMongoDatabase database) => _summaries = database.GetCollection<TripTelemetrySummary>(MongoCollectionNames.TripTelemetrySummaries);
    public async Task<TripTelemetrySummary?> GetByIdAsync(string id, CancellationToken cancellationToken) => await _summaries.Find(s => s.Id == id).FirstOrDefaultAsync(cancellationToken);
    public async Task<TripTelemetrySummary?> GetByTripIdAsync(string tripId, CancellationToken cancellationToken) => await _summaries.Find(s => s.TripId == tripId).FirstOrDefaultAsync(cancellationToken);
    public async Task<TripTelemetrySummary?> GetByUserIdAndTripIdAsync(string userId, string tripId, CancellationToken cancellationToken) => await _summaries.Find(s => s.UserId == userId && s.TripId == tripId).FirstOrDefaultAsync(cancellationToken);
    public async Task<TripTelemetrySummary> UpsertAsync(TripTelemetrySummary summary, CancellationToken cancellationToken)
    {
        TripTelemetrySummary? existing = await GetByUserIdAndTripIdAsync(summary.UserId, summary.TripId, cancellationToken);
        if (existing is not null)
        {
            summary.Id = existing.Id;
            summary.CreatedAtUtc = existing.CreatedAtUtc;
        }

        await _summaries.ReplaceOneAsync(s => s.UserId == summary.UserId && s.TripId == summary.TripId, summary, new ReplaceOptions { IsUpsert = true }, cancellationToken);
        return (await GetByUserIdAndTripIdAsync(summary.UserId, summary.TripId, cancellationToken)) ?? summary;
    }

    public async Task<IReadOnlyList<TripTelemetrySummary>> ListByUserIdAsync(string userId, TelemetrySummaryQuery query, CancellationToken cancellationToken) => await _summaries.Find(BuildFilter(query) & Builders<TripTelemetrySummary>.Filter.Eq(s => s.UserId, userId)).SortByDescending(s => s.LastComputedAtUtc).Skip((query.PageNumber - 1) * query.PageSize).Limit(query.PageSize).ToListAsync(cancellationToken);
    public async Task<long> CountByUserIdAsync(string userId, TelemetrySummaryQuery query, CancellationToken cancellationToken) => await _summaries.CountDocumentsAsync(BuildFilter(query) & Builders<TripTelemetrySummary>.Filter.Eq(s => s.UserId, userId), cancellationToken: cancellationToken);
    public async Task<IReadOnlyList<TripTelemetrySummary>> ListAsync(TelemetrySummaryQuery query, CancellationToken cancellationToken) => await _summaries.Find(BuildFilter(query)).SortByDescending(s => s.LastComputedAtUtc).Skip((query.PageNumber - 1) * query.PageSize).Limit(query.PageSize).ToListAsync(cancellationToken);
    public async Task<long> CountAsync(TelemetrySummaryQuery query, CancellationToken cancellationToken) => await _summaries.CountDocumentsAsync(BuildFilter(query), cancellationToken: cancellationToken);
    private static FilterDefinition<TripTelemetrySummary> BuildFilter(TelemetrySummaryQuery q) { var b = Builders<TripTelemetrySummary>.Filter; var f = b.Empty; if (!string.IsNullOrWhiteSpace(q.UserId)) f &= b.Eq(s => s.UserId, q.UserId); if (!string.IsNullOrWhiteSpace(q.TripId)) f &= b.Eq(s => s.TripId, q.TripId); if (q.TripStatus.HasValue) f &= b.Eq(s => s.TripStatus, q.TripStatus.Value); if (q.SummaryStatus.HasValue) f &= b.Eq(s => s.SummaryStatus, q.SummaryStatus.Value); if (q.DateFrom.HasValue) f &= b.Gte(s => s.LastComputedAtUtc, q.DateFrom.Value); if (q.DateTo.HasValue) f &= b.Lte(s => s.LastComputedAtUtc, q.DateTo.Value); return f; }
}
