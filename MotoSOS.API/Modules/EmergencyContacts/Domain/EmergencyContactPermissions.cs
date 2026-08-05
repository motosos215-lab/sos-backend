namespace MotoSOS.API.Modules.EmergencyContacts.Domain;

public sealed class EmergencyContactPermissions
{
    public bool CanViewRealTimeLocation { get; set; }

    public bool CanReceiveCriticalAlerts { get; set; }

    public bool CanViewIncidentHistory { get; set; }

    public bool CanViewVitalSigns { get; set; }
}
