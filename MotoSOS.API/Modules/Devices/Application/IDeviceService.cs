using MotoSOS.API.Modules.Devices.Contracts;

namespace MotoSOS.API.Modules.Devices.Application;

public interface IDeviceService
{
    Task<GetDevicesResponse> GetMyDevicesAsync(string userId, CancellationToken cancellationToken);
    Task<CreateMobileActivationCodeResponse> CreateMobileActivationCodeAsync(string userId, CancellationToken cancellationToken);
    Task<GetCurrentMobileActivationCodeResponse> GetCurrentMobileActivationCodeAsync(string userId, CancellationToken cancellationToken);
    Task<LinkMobileDeviceResponse> LinkMobileDeviceAsync(string userId, LinkMobileDeviceRequest request, CancellationToken cancellationToken);
    Task<LinkSmartwatchResponse> LinkSmartwatchAsync(string userId, LinkSmartwatchRequest request, CancellationToken cancellationToken);
    Task<HeartbeatDeviceResponse> HeartbeatAsync(string userId, string deviceId, HeartbeatDeviceRequest request, CancellationToken cancellationToken);
    Task RevokeAsync(string userId, string deviceId, CancellationToken cancellationToken);
}
