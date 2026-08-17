using MotoSOS.API.Modules.Trips.Contracts;

namespace MotoSOS.API.Modules.Trips.Application;

public interface ITripRoutePointService
{
    Task<CreateTripRoutePointsBatchResponse> CreateBatchAsync(string userId, string tripId, CreateTripRoutePointsBatchRequest request, CancellationToken cancellationToken);
    Task<GetTripRouteResponse> GetRouteAsync(string userId, string tripId, string? mode, int? pageNumber, int? pageSize, int? maxPoints, CancellationToken cancellationToken);
}
