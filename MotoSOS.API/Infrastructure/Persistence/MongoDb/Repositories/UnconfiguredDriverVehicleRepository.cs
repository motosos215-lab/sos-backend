using MotoSOS.API.Modules.Vehicles.Application;
using MotoSOS.API.Modules.Vehicles.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class UnconfiguredDriverVehicleRepository : IDriverVehicleRepository
{
    public Task<IReadOnlyList<DriverVehicle>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => throw CreateException();

    public Task<DriverVehicle?> GetByIdAsync(string id, CancellationToken cancellationToken) => throw CreateException();

    public Task<int> CountActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => throw CreateException();

    public Task AddAsync(DriverVehicle vehicle, CancellationToken cancellationToken) => throw CreateException();

    public Task UpdateAsync(DriverVehicle vehicle, CancellationToken cancellationToken) => throw CreateException();

    private static InvalidOperationException CreateException() => new("MongoDB is not configured for driver vehicle persistence.");
}
