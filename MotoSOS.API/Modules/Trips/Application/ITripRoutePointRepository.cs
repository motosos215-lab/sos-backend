using MotoSOS.API.Modules.Trips.Domain;

namespace MotoSOS.API.Modules.Trips.Application;

public interface ITripRoutePointRepository
{
    Task<IReadOnlyList<TripRoutePoint>> GetByTripIdAndClientIdsAsync(string tripId, IReadOnlyCollection<string> clientRoutePointIds, CancellationToken cancellationToken);
    Task<(TripRoutePoint RoutePoint, bool IsDuplicate)> AddOrGetDuplicateAsync(TripRoutePoint routePoint, CancellationToken cancellationToken);
    Task<IReadOnlyList<TripRoutePoint>> ListByUserIdAndTripIdAsync(string userId, string tripId, CancellationToken cancellationToken);
}
