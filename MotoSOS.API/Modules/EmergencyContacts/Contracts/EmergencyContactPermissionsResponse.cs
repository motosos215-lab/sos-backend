namespace MotoSOS.API.Modules.EmergencyContacts.Contracts;

public sealed record EmergencyContactPermissionsResponse(
    bool CanViewRealTimeLocation,
    bool CanReceiveCriticalAlerts,
    bool CanViewIncidentHistory,
    bool CanViewVitalSigns);
