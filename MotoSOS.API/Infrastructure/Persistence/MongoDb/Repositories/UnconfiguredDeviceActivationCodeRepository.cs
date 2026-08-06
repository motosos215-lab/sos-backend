using MotoSOS.API.Modules.Devices.Application;
using MotoSOS.API.Modules.Devices.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class UnconfiguredDeviceActivationCodeRepository : IDeviceActivationCodeRepository
{
    public Task<IReadOnlyList<DeviceActivationCode>> GetActiveByUserIdAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken) => throw CreateException();
    public Task<DeviceActivationCode?> GetActiveCurrentByUserIdAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken) => throw CreateException();
    public Task<DeviceActivationCode?> GetByCodeAsync(string code, CancellationToken cancellationToken) => throw CreateException();
    public Task AddAsync(DeviceActivationCode code, CancellationToken cancellationToken) => throw CreateException();
    public Task UpdateAsync(DeviceActivationCode code, CancellationToken cancellationToken) => throw CreateException();
    private static InvalidOperationException CreateException() => new("MongoDB is not configured.");
}
