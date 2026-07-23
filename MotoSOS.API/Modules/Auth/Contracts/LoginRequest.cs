namespace MotoSOS.API.Modules.Auth.Contracts;

public sealed record LoginRequest(string Email, string Password, bool RememberMe = false);
