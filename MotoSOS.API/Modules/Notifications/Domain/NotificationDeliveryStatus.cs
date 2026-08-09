namespace MotoSOS.API.Modules.Notifications.Domain;

public enum NotificationDeliveryStatus
{
    Prepared = 1,
    SimulatedSent = 2,
    Failed = 3,
    Cancelled = 4
}
