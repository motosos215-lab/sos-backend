namespace MotoSOS.API.Modules.EmergencyStatus.Application;

public enum EmergencyOverallStatus
{
    Active = 1,
    AwaitingAcknowledgement = 2,
    Acknowledged = 3,
    Declined = 4,
    Closed = 5,
    Cancelled = 6,
    Unknown = 7
}
