using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.Devices.Contracts;
using MotoSOS.API.Modules.Devices.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Modules.Devices.Application;

public sealed class DeviceService : IDeviceService
{
    private static readonly TimeSpan ActivationCodeLifetime = TimeSpan.FromMinutes(15);

    private readonly IUserRepository _users;
    private readonly IDeviceActivationCodeRepository _activationCodes;
    private readonly IUserDeviceRepository _devices;
    private readonly IActivationCodeGenerator _codeGenerator;
    private readonly IDeviceIdentifierHasher _identifierHasher;
    private readonly IClock _clock;

    public DeviceService(
        IUserRepository users,
        IDeviceActivationCodeRepository activationCodes,
        IUserDeviceRepository devices,
        IActivationCodeGenerator codeGenerator,
        IDeviceIdentifierHasher identifierHasher,
        IClock clock)
    {
        _users = users;
        _activationCodes = activationCodes;
        _devices = devices;
        _codeGenerator = codeGenerator;
        _identifierHasher = identifierHasher;
        _clock = clock;
    }

    public async Task<GetDevicesResponse> GetMyDevicesAsync(string userId, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        IReadOnlyList<UserDevice> devices = await _devices.GetActiveByUserIdAsync(user.Id, cancellationToken);
        return new GetDevicesResponse(devices.Select(ToResponse).ToArray());
    }

    public async Task<CreateMobileActivationCodeResponse> CreateMobileActivationCodeAsync(string userId, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        DateTimeOffset now = _clock.UtcNow;
        IReadOnlyList<DeviceActivationCode> previousCodes = await _activationCodes.GetActiveByUserIdAsync(user.Id, now, cancellationToken);

        foreach (DeviceActivationCode previousCode in previousCodes)
        {
            previousCode.IsRevoked = true;
            previousCode.RevokedAtUtc = now;
            previousCode.UpdatedAtUtc = now;
            await _activationCodes.UpdateAsync(previousCode, cancellationToken);
        }

        var activationCode = new DeviceActivationCode
        {
            UserId = user.Id,
            Code = _codeGenerator.CreateCode(),
            ExpiresAtUtc = now.Add(ActivationCodeLifetime),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _activationCodes.AddAsync(activationCode, cancellationToken);
        return new CreateMobileActivationCodeResponse(ToActivationCodeResponse(activationCode));
    }

    public async Task<GetCurrentMobileActivationCodeResponse> GetCurrentMobileActivationCodeAsync(string userId, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        DeviceActivationCode? activationCode = await _activationCodes.GetActiveCurrentByUserIdAsync(user.Id, _clock.UtcNow, cancellationToken);
        return new GetCurrentMobileActivationCodeResponse(activationCode is null ? null : ToActivationCodeResponse(activationCode));
    }

    public async Task<LinkMobileDeviceResponse> LinkMobileDeviceAsync(string userId, LinkMobileDeviceRequest request, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        DateTimeOffset now = _clock.UtcNow;
        DeviceActivationCode activationCode = await GetValidActivationCodeAsync(user.Id, request.Code, now, cancellationToken);
        int activeMobileApps = await _devices.CountActiveLinkedByUserIdAndTypeAsync(user.Id, DeviceType.MobileApp, cancellationToken);

        if (activeMobileApps >= 1)
        {
            throw new PlanLimitExceededAppException("Basic plan allows only one active linked mobile app.");
        }

        var device = new UserDevice
        {
            UserId = user.Id,
            DeviceType = DeviceType.MobileApp,
            DeviceName = NormalizeRequired(request.DeviceName),
            Platform = ParseEnum<DevicePlatform>(request.Platform) ?? DevicePlatform.Unknown,
            Manufacturer = NormalizeOptional(request.Manufacturer),
            Model = NormalizeOptional(request.Model),
            OperatingSystemVersion = NormalizeOptional(request.OperatingSystemVersion),
            AppVersion = NormalizeOptional(request.AppVersion),
            DeviceIdentifierHash = _identifierHasher.Hash(request.DeviceIdentifier),
            LinkStatus = DeviceLinkStatus.Linked,
            ConnectionStatus = DeviceConnectionStatus.Online,
            LastSyncAtUtc = now,
            LastHeartbeatAtUtc = now,
            LinkedAtUtc = now,
            IsPrimary = activeMobileApps == 0,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        activationCode.IsUsed = true;
        activationCode.UsedAtUtc = now;
        activationCode.UpdatedAtUtc = now;

        await _activationCodes.UpdateAsync(activationCode, cancellationToken);
        await _devices.AddAsync(device, cancellationToken);

        return new LinkMobileDeviceResponse(ToResponse(device));
    }

    public async Task<LinkSmartwatchResponse> LinkSmartwatchAsync(string userId, LinkSmartwatchRequest request, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        UserDevice parent = await GetOwnedActiveLinkedDeviceAsync(user.Id, NormalizeRequired(request.ParentDeviceId), cancellationToken);

        if (parent.DeviceType != DeviceType.MobileApp)
        {
            throw new NotFoundAppException("Parent mobile device was not found.");
        }

        DateTimeOffset now = _clock.UtcNow;
        string? identifierHash = _identifierHasher.Hash(request.DeviceIdentifier);
        UserDevice? device = identifierHash is null
            ? null
            : await _devices.GetByDeviceIdentifierHashAsync(user.Id, identifierHash, DeviceType.Smartwatch, cancellationToken);

        bool isNew = device is null;
        device ??= new UserDevice { UserId = user.Id, DeviceType = DeviceType.Smartwatch, CreatedAtUtc = now };

        device.DeviceName = NormalizeRequired(request.DeviceName);
        device.Platform = ParseEnum<DevicePlatform>(request.Platform) ?? DevicePlatform.Unknown;
        device.Manufacturer = NormalizeOptional(request.Manufacturer);
        device.Model = NormalizeOptional(request.Model);
        device.OperatingSystemVersion = NormalizeOptional(request.OperatingSystemVersion);
        device.AppVersion = NormalizeOptional(request.AppVersion);
        device.DeviceIdentifierHash = identifierHash;
        device.ParentDeviceId = parent.Id;
        device.LinkStatus = DeviceLinkStatus.Linked;
        device.ConnectionStatus = DeviceConnectionStatus.Online;
        device.BatteryLevel = request.BatteryLevel;
        device.LastSyncAtUtc = now;
        device.LastHeartbeatAtUtc = now;
        device.LinkedAtUtc ??= now;
        device.RevokedAtUtc = null;
        device.IsActive = true;
        device.UpdatedAtUtc = now;

        if (isNew)
        {
            await _devices.AddAsync(device, cancellationToken);
        }
        else
        {
            await _devices.UpdateAsync(device, cancellationToken);
        }

        return new LinkSmartwatchResponse(ToResponse(device));
    }

    public async Task<HeartbeatDeviceResponse> HeartbeatAsync(string userId, string deviceId, HeartbeatDeviceRequest request, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        UserDevice device = await GetOwnedActiveDeviceAsync(user.Id, deviceId, cancellationToken);
        DateTimeOffset now = _clock.UtcNow;

        if (request.BatteryLevel.HasValue)
        {
            device.BatteryLevel = request.BatteryLevel;
        }

        if (!string.IsNullOrWhiteSpace(request.ConnectionStatus))
        {
            device.ConnectionStatus = ParseEnum<DeviceConnectionStatus>(request.ConnectionStatus) ?? device.ConnectionStatus;
        }

        if (!string.IsNullOrWhiteSpace(request.AppVersion))
        {
            device.AppVersion = request.AppVersion.Trim();
        }

        device.LastHeartbeatAtUtc = now;
        device.LastSyncAtUtc = now;
        device.UpdatedAtUtc = now;
        await _devices.UpdateAsync(device, cancellationToken);

        return new HeartbeatDeviceResponse(ToResponse(device));
    }

    public async Task RevokeAsync(string userId, string deviceId, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        UserDevice device = await GetOwnedActiveDeviceAsync(user.Id, deviceId, cancellationToken);
        DateTimeOffset now = _clock.UtcNow;

        RevokeDevice(device, now);
        await _devices.UpdateAsync(device, cancellationToken);

        if (device.DeviceType == DeviceType.MobileApp)
        {
            IReadOnlyList<UserDevice> dependentDevices = await _devices.GetActiveByParentDeviceIdAsync(device.Id, cancellationToken);
            foreach (UserDevice dependentDevice in dependentDevices)
            {
                RevokeDevice(dependentDevice, now);
                await _devices.UpdateAsync(dependentDevice, cancellationToken);
            }
        }
    }

    private async Task<User> GetRiderUserAsync(string userId, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedAppException("Invalid authentication credentials.");
        }

        if (user.Role != UserRole.Rider)
        {
            throw new ForbiddenAppException("This devices flow is available only for riders.");
        }

        return user;
    }

    private async Task<DeviceActivationCode> GetValidActivationCodeAsync(string userId, string? code, DateTimeOffset now, CancellationToken cancellationToken)
    {
        DeviceActivationCode? activationCode = await _activationCodes.GetByCodeAsync(NormalizeRequired(code), cancellationToken);
        if (activationCode is null || activationCode.UserId != userId || activationCode.IsUsed || activationCode.IsRevoked || activationCode.ExpiresAtUtc <= now)
        {
            throw new ActivationCodeInvalidAppException("Activation code is invalid.");
        }

        return activationCode;
    }

    private async Task<UserDevice> GetOwnedActiveDeviceAsync(string userId, string deviceId, CancellationToken cancellationToken)
    {
        UserDevice? device = await _devices.GetByIdAsync(deviceId, cancellationToken);
        if (device is null || device.UserId != userId || !device.IsActive)
        {
            throw new NotFoundAppException("Device was not found.");
        }

        return device;
    }

    private async Task<UserDevice> GetOwnedActiveLinkedDeviceAsync(string userId, string deviceId, CancellationToken cancellationToken)
    {
        UserDevice device = await GetOwnedActiveDeviceAsync(userId, deviceId, cancellationToken);
        if (device.LinkStatus != DeviceLinkStatus.Linked)
        {
            throw new NotFoundAppException("Device was not found.");
        }

        return device;
    }

    private static void RevokeDevice(UserDevice device, DateTimeOffset now)
    {
        device.IsActive = false;
        device.LinkStatus = DeviceLinkStatus.Revoked;
        device.ConnectionStatus = DeviceConnectionStatus.Offline;
        device.RevokedAtUtc = now;
        device.UpdatedAtUtc = now;
    }

    private static DeviceResponse ToResponse(UserDevice device) => new(
        device.Id,
        device.UserId,
        device.DeviceType.ToString(),
        device.DeviceName,
        device.Platform.ToString(),
        device.Manufacturer,
        device.Model,
        device.OperatingSystemVersion,
        device.AppVersion,
        device.ParentDeviceId,
        device.LinkStatus.ToString(),
        device.ConnectionStatus.ToString(),
        device.BatteryLevel,
        device.LastSyncAtUtc,
        device.LastHeartbeatAtUtc,
        device.LinkedAtUtc,
        device.RevokedAtUtc,
        device.IsPrimary,
        device.IsActive,
        device.CreatedAtUtc,
        device.UpdatedAtUtc);

    private static MobileActivationCodeResponse ToActivationCodeResponse(DeviceActivationCode activationCode) =>
        new(activationCode.Code, activationCode.ExpiresAtUtc);

    private static TEnum? ParseEnum<TEnum>(string? value)
        where TEnum : struct => Enum.TryParse(value, ignoreCase: true, out TEnum result) ? result : null;

    private static string NormalizeRequired(string? value) => value?.Trim() ?? string.Empty;

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
