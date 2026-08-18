using MotoSOS.API.Modules.Auth.Sessions.Contracts;

namespace MotoSOS.API.Modules.Auth.Contracts;

public sealed record LoginRequest(string Email, string Password, bool RememberMe = false, ClientDeviceRequest? ClientDevice = null, string? ClientType = null);
