namespace MotoSOS.API.Modules.LocationSharing.Domain;

public enum LocationSharingDeactivationReason
{
    IncidentClosed = 1,
    FalsePositiveCancelled = 2,
    AlertCancelled = 3,
    ManualStop = 4,
    Unknown = 5
}
