namespace MotoSOS.API.Modules.EmergencyContacts.Contracts;

public sealed record UpdateEmergencyContactRequest(
    string? FullName,
    string? Relationship,
    string? PhoneNumber,
    string? Email,
    int? Priority,
    EmergencyContactPermissionsRequest? Permissions,
    string? SaveMode);
