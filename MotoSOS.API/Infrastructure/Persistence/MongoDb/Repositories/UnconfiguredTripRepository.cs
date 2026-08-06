using MotoSOS.API.Modules.Trips.Application;
using MotoSOS.API.Modules.Trips.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class UnconfiguredTripRepository : ITripRepository
{
    public Task<Trip?> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<Trip?>(null);
    public Task<Trip?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult<Trip?>(null);
    public Task<IReadOnlyList<Trip>> ListByUserIdAsync(string userId, TripStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Trip>>([]);
    public Task<long> CountByUserIdAsync(string userId, TripStatus? status, CancellationToken cancellationToken) => Task.FromResult(0L);
    public Task AddAsync(Trip trip, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task UpdateAsync(Trip trip, CancellationToken cancellationToken) => Task.CompletedTask;
}
