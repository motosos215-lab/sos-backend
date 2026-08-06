using MotoSOS.API.Modules.Devices.Application;
using MotoSOS.API.Modules.Devices.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class UnconfiguredUserDeviceRepository : IUserDeviceRepository
{
    public Task<IReadOnlyList<UserDevice>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => throw CreateException();
    public Task<IReadOnlyList<UserDevice>> GetActiveByParentDeviceIdAsync(string parentDeviceId, CancellationToken cancellationToken) => throw CreateException();
    public Task<UserDevice?> GetByIdAsync(string id, CancellationToken cancellationToken) => throw CreateException();
    public Task<UserDevice?> GetByDeviceIdentifierHashAsync(string userId, string hash, DeviceType deviceType, CancellationToken cancellationToken) => throw CreateException();
    public Task<int> CountActiveLinkedByUserIdAndTypeAsync(string userId, DeviceType deviceType, CancellationToken cancellationToken) => throw CreateException();
    public Task<bool> HasActiveLinkedMobileAppAsync(string userId, CancellationToken cancellationToken) => throw CreateException();
    public Task AddAsync(UserDevice device, CancellationToken cancellationToken) => throw CreateException();
    public Task UpdateAsync(UserDevice device, CancellationToken cancellationToken) => throw CreateException();
    private static InvalidOperationException CreateException() => new("MongoDB is not configured.");
}
