using MotoSOS.API.Modules.Trips.Domain;

namespace MotoSOS.API.Modules.Trips.Application;

public interface ITripRepository
{
    Task<Trip?> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken);
    Task<Trip?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Trip>> ListByUserIdAsync(string userId, TripStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken);
    Task<long> CountByUserIdAsync(string userId, TripStatus? status, CancellationToken cancellationToken);
    Task AddAsync(Trip trip, CancellationToken cancellationToken);
    Task UpdateAsync(Trip trip, CancellationToken cancellationToken);
}
