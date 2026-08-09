using MotoSOS.API.Modules.Trips.Contracts;

namespace MotoSOS.API.Modules.Trips.Application;

public interface ITripService
{
    Task<GetActiveTripResponse> GetActiveAsync(string userId, CancellationToken cancellationToken);
    Task<StartTripResponse> StartAsync(string userId, StartTripRequest request, CancellationToken cancellationToken);
    Task<FinishTripResponse> FinishAsync(string userId, string tripId, FinishTripRequest request, CancellationToken cancellationToken);
    Task<GetTripResponse> GetAsync(string userId, string tripId, CancellationToken cancellationToken);
    Task<GetTripsResponse> ListAsync(string userId, string? status, int? pageNumber, int? pageSize, CancellationToken cancellationToken);
}
