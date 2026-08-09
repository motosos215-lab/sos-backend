using MongoDB.Driver;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Collections;
using MotoSOS.API.Modules.Incidents.Application;
using MotoSOS.API.Modules.Incidents.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class MongoIncidentRepository : IIncidentRepository
{
    private readonly IMongoCollection<Incident> _incidents;

    public MongoIncidentRepository(IMongoDatabase database) => _incidents = database.GetCollection<Incident>(MongoCollectionNames.Incidents);

    public async Task<Incident?> GetByIdAsync(string id, CancellationToken cancellationToken) => await _incidents.Find(incident => incident.Id == id).FirstOrDefaultAsync(cancellationToken);
    public async Task<Incident?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) => await _incidents.Find(incident => incident.IdempotencyKey == idempotencyKey).FirstOrDefaultAsync(cancellationToken);

    public async Task<(Incident Incident, bool IsDuplicate)> AddOrGetDuplicateAsync(Incident incident, CancellationToken cancellationToken)
    {
        Incident? existing = await GetByIdempotencyKeyAsync(incident.IdempotencyKey, cancellationToken);
        if (existing is not null) return (existing, true);
        try
        {
            await _incidents.InsertOneAsync(incident, cancellationToken: cancellationToken);
            return (incident, false);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            existing = await GetByIdempotencyKeyAsync(incident.IdempotencyKey, cancellationToken);
            if (existing is not null) return (existing, true);
            throw;
        }
    }

    public async Task<IReadOnlyList<Incident>> ListByUserIdAsync(string userId, IncidentStatus? status, string? tripId, int pageNumber, int pageSize, CancellationToken cancellationToken) =>
        await _incidents.Find(BuildUserFilter(userId, status, tripId)).SortByDescending(i => i.CreatedAtUtc).Skip((pageNumber - 1) * pageSize).Limit(pageSize).ToListAsync(cancellationToken);

    public async Task<long> CountByUserIdAsync(string userId, IncidentStatus? status, string? tripId, CancellationToken cancellationToken) =>
        await _incidents.CountDocumentsAsync(BuildUserFilter(userId, status, tripId), cancellationToken: cancellationToken);

    public async Task UpdateAsync(Incident incident, CancellationToken cancellationToken) => await _incidents.ReplaceOneAsync(existing => existing.Id == incident.Id, incident, cancellationToken: cancellationToken);

    private static FilterDefinition<Incident> BuildUserFilter(string userId, IncidentStatus? status, string? tripId)
    {
        FilterDefinitionBuilder<Incident> builder = Builders<Incident>.Filter;
        FilterDefinition<Incident> filter = builder.Eq(i => i.UserId, userId);
        if (status.HasValue) filter &= builder.Eq(i => i.Status, status.Value);
        if (!string.IsNullOrWhiteSpace(tripId)) filter &= builder.Eq(i => i.TripId, tripId);
        return filter;
    }
}
