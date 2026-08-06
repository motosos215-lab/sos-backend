using MotoSOS.API.Modules.Devices.Domain;

namespace MotoSOS.API.Modules.Devices.Application;

public interface IUserDeviceRepository
{
    Task<IReadOnlyList<UserDevice>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserDevice>> GetActiveByParentDeviceIdAsync(string parentDeviceId, CancellationToken cancellationToken);
    Task<UserDevice?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task<UserDevice?> GetByDeviceIdentifierHashAsync(string userId, string hash, DeviceType deviceType, CancellationToken cancellationToken);
    Task<int> CountActiveLinkedByUserIdAndTypeAsync(string userId, DeviceType deviceType, CancellationToken cancellationToken);
    Task<bool> HasActiveLinkedMobileAppAsync(string userId, CancellationToken cancellationToken);
    Task AddAsync(UserDevice device, CancellationToken cancellationToken);
    Task UpdateAsync(UserDevice device, CancellationToken cancellationToken);
}
