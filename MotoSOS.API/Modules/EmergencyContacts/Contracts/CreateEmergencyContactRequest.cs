namespace MotoSOS.API.Modules.EmergencyContacts.Contracts;

public sealed record CreateEmergencyContactRequest(
    string? FullName,
    string? Relationship,
    string? PhoneNumber,
    string? Email,
    int? Priority,
    EmergencyContactPermissionsRequest? Permissions,
    string? SaveMode);
