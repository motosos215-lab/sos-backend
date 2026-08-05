using MotoSOS.API.Modules.Profiles.Application;
using MotoSOS.API.Modules.Profiles.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class UnconfiguredDriverProfileRepository : IDriverProfileRepository
{
    public Task<DriverProfile?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) => throw CreateException();

    public Task AddAsync(DriverProfile profile, CancellationToken cancellationToken) => throw CreateException();

    public Task UpdateAsync(DriverProfile profile, CancellationToken cancellationToken) => throw CreateException();

    private static InvalidOperationException CreateException() => new("MongoDB is not configured for driver profile persistence.");
}
