namespace MotoSOS.API.Modules.Devices.Contracts;

public sealed record HeartbeatDeviceRequest(int? BatteryLevel, string? ConnectionStatus, string? AppVersion);
