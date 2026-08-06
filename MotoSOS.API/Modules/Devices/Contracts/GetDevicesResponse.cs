namespace MotoSOS.API.Modules.Devices.Contracts;

public sealed record GetDevicesResponse(IReadOnlyList<DeviceResponse> Devices);
