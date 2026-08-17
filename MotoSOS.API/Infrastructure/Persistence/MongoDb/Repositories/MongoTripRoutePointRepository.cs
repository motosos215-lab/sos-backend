using MongoDB.Driver;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Collections;
using MotoSOS.API.Modules.Trips.Application;
using MotoSOS.API.Modules.Trips.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class MongoTripRoutePointRepository : ITripRoutePointRepository
{
    private readonly IMongoCollection<TripRoutePoint> _routePoints;

    public MongoTripRoutePointRepository(IMongoDatabase database)
    {
        _routePoints = database.GetCollection<TripRoutePoint>(MongoCollectionNames.TripRoutePoints);
    }

    public async Task<IReadOnlyList<TripRoutePoint>> GetByTripIdAndClientIdsAsync(string tripId, IReadOnlyCollection<string> clientRoutePointIds, CancellationToken cancellationToken) =>
        await _routePoints.Find(point => point.TripId == tripId && clientRoutePointIds.Contains(point.ClientRoutePointId)).ToListAsync(cancellationToken);

    public async Task<(TripRoutePoint RoutePoint, bool IsDuplicate)> AddOrGetDuplicateAsync(TripRoutePoint routePoint, CancellationToken cancellationToken)
    {
        TripRoutePoint? existing = await GetByTripIdAndClientIdAsync(routePoint.TripId, routePoint.ClientRoutePointId, cancellationToken);
        if (existing is not null) return (existing, true);

        try
        {
            await _routePoints.InsertOneAsync(routePoint, cancellationToken: cancellationToken);
            return (routePoint, false);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            existing = await GetByTripIdAndClientIdAsync(routePoint.TripId, routePoint.ClientRoutePointId, cancellationToken);
            if (existing is not null) return (existing, true);
            throw;
        }
    }

    public async Task<IReadOnlyList<TripRoutePoint>> ListByUserIdAndTripIdAsync(string userId, string tripId, CancellationToken cancellationToken) =>
        await _routePoints.Find(point => point.UserId == userId && point.TripId == tripId).SortBy(point => point.Sequence).ToListAsync(cancellationToken);

    private async Task<TripRoutePoint?> GetByTripIdAndClientIdAsync(string tripId, string clientRoutePointId, CancellationToken cancellationToken) =>
        await _routePoints.Find(point => point.TripId == tripId && point.ClientRoutePointId == clientRoutePointId).FirstOrDefaultAsync(cancellationToken);
}
