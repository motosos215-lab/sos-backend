namespace MotoSOS.API.Modules.Users.Contracts;

public sealed record UserResponse(
    string Id,
    string Email,
    string FullName,
    string? PhoneNumber,
    string Role,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    DateTimeOffset? LastLoginAtUtc);
