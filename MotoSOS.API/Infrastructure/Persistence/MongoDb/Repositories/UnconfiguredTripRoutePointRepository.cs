using MotoSOS.API.Modules.Trips.Application;
using MotoSOS.API.Modules.Trips.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class UnconfiguredTripRoutePointRepository : ITripRoutePointRepository
{
    public Task<IReadOnlyList<TripRoutePoint>> GetByTripIdAndClientIdsAsync(string tripId, IReadOnlyCollection<string> clientRoutePointIds, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TripRoutePoint>>([]);
    public Task<(TripRoutePoint RoutePoint, bool IsDuplicate)> AddOrGetDuplicateAsync(TripRoutePoint routePoint, CancellationToken cancellationToken) => Task.FromResult((routePoint, false));
    public Task<IReadOnlyList<TripRoutePoint>> ListByUserIdAndTripIdAsync(string userId, string tripId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TripRoutePoint>>([]);
}
