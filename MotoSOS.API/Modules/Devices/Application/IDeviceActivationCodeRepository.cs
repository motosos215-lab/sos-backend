using MotoSOS.API.Modules.Devices.Domain;

namespace MotoSOS.API.Modules.Devices.Application;

public interface IDeviceActivationCodeRepository
{
    Task<IReadOnlyList<DeviceActivationCode>> GetActiveByUserIdAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<DeviceActivationCode?> GetActiveCurrentByUserIdAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<DeviceActivationCode?> GetByCodeAsync(string code, CancellationToken cancellationToken);
    Task AddAsync(DeviceActivationCode code, CancellationToken cancellationToken);
    Task UpdateAsync(DeviceActivationCode code, CancellationToken cancellationToken);
}
