namespace MotoSOS.API.Modules.Devices.Contracts;

public sealed record LinkMobileDeviceRequest(
    string? Code,
    string? DeviceName,
    string? Platform,
    string? Manufacturer,
    string? Model,
    string? OperatingSystemVersion,
    string? AppVersion,
    string? DeviceIdentifier);
