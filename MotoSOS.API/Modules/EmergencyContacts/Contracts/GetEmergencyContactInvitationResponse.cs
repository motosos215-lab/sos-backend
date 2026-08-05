namespace MotoSOS.API.Modules.EmergencyContacts.Contracts;

public sealed record GetEmergencyContactInvitationResponse(EmergencyContactInvitationDetails Invitation);

public sealed record EmergencyContactInvitationDetails(
    string DriverFullName,
    string ContactFullName,
    EmergencyContactPermissionsResponse Permissions,
    DateTimeOffset ExpiresAtUtc,
    string Status);
