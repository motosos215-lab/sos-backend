namespace MotoSOS.API.Modules.Auth.Sessions.Contracts;

public sealed record ClientDeviceRequest(string ClientDeviceId, string DeviceName, string Platform, string? OsVersion = null, string? AppVersion = null);
