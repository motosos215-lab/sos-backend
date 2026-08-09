namespace MotoSOS.API.Modules.AuditLogs.Domain;

public enum AuditModule
{
    Unknown = 0,
    Auth = 1,
    Incidents = 2,
    AlertDispatch = 3,
    Notifications = 4,
    NotificationOutbox = 5,
    AlertAcknowledgements = 6,
    EmergencyResolution = 7,
    OfflineProcessing = 8,
    OperationalDashboard = 9
}
