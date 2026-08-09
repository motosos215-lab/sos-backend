namespace MotoSOS.API.Modules.EmergencyResolution.Domain;

public enum EmergencyResolutionOutcome
{
    Unknown = 0,
    RealEmergency = 1,
    FalsePositive = 2,
    UserSafe = 3,
    Assisted = 4,
    Cancelled = 5
}
