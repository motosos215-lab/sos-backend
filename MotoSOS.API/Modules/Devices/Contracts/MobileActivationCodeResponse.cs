namespace MotoSOS.API.Modules.Devices.Contracts;

public sealed record MobileActivationCodeResponse(string Code, DateTimeOffset ExpiresAtUtc);
