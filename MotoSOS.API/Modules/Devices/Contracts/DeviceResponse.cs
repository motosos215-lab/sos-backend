namespace MotoSOS.API.Modules.Devices.Contracts;

public sealed record DeviceResponse(
    string Id,
    string UserId,
    string DeviceType,
    string DeviceName,
    string Platform,
    string? Manufacturer,
    string? Model,
    string? OperatingSystemVersion,
    string? AppVersion,
    string? ParentDeviceId,
    string LinkStatus,
    string ConnectionStatus,
    int? BatteryLevel,
    DateTimeOffset? LastSyncAtUtc,
    DateTimeOffset? LastHeartbeatAtUtc,
    DateTimeOffset? LinkedAtUtc,
    DateTimeOffset? RevokedAtUtc,
    bool IsPrimary,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);
