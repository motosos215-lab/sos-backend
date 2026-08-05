namespace MotoSOS.API.Modules.EmergencyContacts.Contracts;

public sealed record EmergencyContactResponse(
    string Id,
    string UserId,
    string? FullName,
    string? Relationship,
    string? PhoneNumber,
    string? Email,
    int? Priority,
    string InvitationStatus,
    string? LinkingCode,
    DateTimeOffset? LinkingCodeExpiresAtUtc,
    string? LinkedUserId,
    EmergencyContactPermissionsResponse Permissions,
    bool IsPrimary,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    DateTimeOffset? InvitedAtUtc,
    DateTimeOffset? LinkedAtUtc,
    DateTimeOffset? RevokedAtUtc);
