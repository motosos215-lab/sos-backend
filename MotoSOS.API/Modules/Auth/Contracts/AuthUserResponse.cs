namespace MotoSOS.API.Modules.Auth.Contracts;

public sealed record AuthUserResponse(
    string Id,
    string Email,
    string FullName,
    string? PhoneNumber,
    string Role,
    bool IsActive);
