namespace MotoSOS.API.Modules.Auth.Contracts;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string FullName,
    string? PhoneNumber);
