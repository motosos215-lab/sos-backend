namespace MotoSOS.API.Modules.EmergencyContacts.Contracts;

public sealed record GetEmergencyContactsResponse(IReadOnlyList<EmergencyContactResponse> Contacts);
