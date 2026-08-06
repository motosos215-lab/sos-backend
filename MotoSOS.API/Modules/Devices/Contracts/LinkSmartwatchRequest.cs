namespace MotoSOS.API.Modules.Devices.Contracts;

public sealed record LinkSmartwatchRequest(
    string? ParentDeviceId,
    string? DeviceName,
    string? Platform,
    string? Manufacturer,
    string? Model,
    string? OperatingSystemVersion,
    string? AppVersion,
    string? DeviceIdentifier,
    int? BatteryLevel);
