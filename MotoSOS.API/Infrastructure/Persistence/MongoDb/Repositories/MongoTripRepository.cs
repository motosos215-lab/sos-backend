using MongoDB.Driver;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Collections;
using MotoSOS.API.Modules.Trips.Application;
using MotoSOS.API.Modules.Trips.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class MongoTripRepository : ITripRepository
{
    private readonly IMongoCollection<Trip> _trips;

    public MongoTripRepository(IMongoDatabase database)
    {
        _trips = database.GetCollection<Trip>(MongoCollectionNames.Trips);
    }

    public async Task<Trip?> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) =>
        await _trips.Find(trip => trip.UserId == userId && trip.Status == TripStatus.Active).FirstOrDefaultAsync(cancellationToken);

    public async Task<Trip?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
        await _trips.Find(trip => trip.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Trip>> ListByUserIdAsync(string userId, TripStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        FilterDefinition<Trip> filter = BuildUserFilter(userId, status);
        return await _trips.Find(filter)
            .SortByDescending(trip => trip.StartedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<long> CountByUserIdAsync(string userId, TripStatus? status, CancellationToken cancellationToken) =>
        await _trips.CountDocumentsAsync(BuildUserFilter(userId, status), cancellationToken: cancellationToken);

    public async Task AddAsync(Trip trip, CancellationToken cancellationToken) =>
        await _trips.InsertOneAsync(trip, cancellationToken: cancellationToken);

    public async Task UpdateAsync(Trip trip, CancellationToken cancellationToken) =>
        await _trips.ReplaceOneAsync(existing => existing.Id == trip.Id, trip, cancellationToken: cancellationToken);

    private static FilterDefinition<Trip> BuildUserFilter(string userId, TripStatus? status)
    {
        FilterDefinitionBuilder<Trip> builder = Builders<Trip>.Filter;
        FilterDefinition<Trip> filter = builder.Eq(trip => trip.UserId, userId);
        return status.HasValue ? filter & builder.Eq(trip => trip.Status, status.Value) : filter;
    }
}
