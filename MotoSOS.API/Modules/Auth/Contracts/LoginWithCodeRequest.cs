using MotoSOS.API.Modules.Auth.Sessions.Contracts;

namespace MotoSOS.API.Modules.Auth.Contracts;

public sealed record LoginWithCodeRequest(string Email, string Code, ClientDeviceRequest? ClientDevice = null, string? ClientType = null);
