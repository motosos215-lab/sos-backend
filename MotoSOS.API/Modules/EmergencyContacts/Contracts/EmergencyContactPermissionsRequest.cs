namespace MotoSOS.API.Modules.EmergencyContacts.Contracts;

public sealed record EmergencyContactPermissionsRequest(
    bool CanViewRealTimeLocation,
    bool CanReceiveCriticalAlerts,
    bool CanViewIncidentHistory,
    bool CanViewVitalSigns);
