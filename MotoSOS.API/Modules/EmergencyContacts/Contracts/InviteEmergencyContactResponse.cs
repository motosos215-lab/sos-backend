namespace MotoSOS.API.Modules.EmergencyContacts.Contracts;

public sealed record InviteEmergencyContactResponse(EmergencyContactInvitationResponse Contact);

public sealed record EmergencyContactInvitationResponse(
    string Id,
    string InvitationStatus,
    string LinkingCode,
    DateTimeOffset LinkingCodeExpiresAtUtc);
