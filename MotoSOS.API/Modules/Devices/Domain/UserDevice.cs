using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MotoSOS.API.Modules.Devices.Domain;

public sealed class UserDevice
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string UserId { get; set; } = string.Empty;
    public DeviceType DeviceType { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public DevicePlatform Platform { get; set; } = DevicePlatform.Unknown;
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? OperatingSystemVersion { get; set; }
    public string? AppVersion { get; set; }
    public string? DeviceIdentifierHash { get; set; }
    public string? ParentDeviceId { get; set; }
    public DeviceLinkStatus LinkStatus { get; set; } = DeviceLinkStatus.Pending;
    public DeviceConnectionStatus ConnectionStatus { get; set; } = DeviceConnectionStatus.Unknown;
    public int? BatteryLevel { get; set; }
    public DateTimeOffset? LastSyncAtUtc { get; set; }
    public DateTimeOffset? LastHeartbeatAtUtc { get; set; }
    public DateTimeOffset? LinkedAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
