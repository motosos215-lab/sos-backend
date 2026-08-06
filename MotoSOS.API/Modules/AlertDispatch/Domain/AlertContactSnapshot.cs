using MotoSOS.API.Modules.EmergencyContacts.Domain;

namespace MotoSOS.API.Modules.AlertDispatch.Domain;

public sealed class AlertContactSnapshot
{
    public string EmergencyContactId { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Relationship { get; set; }
    public int? Priority { get; set; }
    public EmergencyContactInvitationStatus InvitationStatus { get; set; }
}
