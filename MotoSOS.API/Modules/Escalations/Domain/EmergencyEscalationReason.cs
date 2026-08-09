namespace MotoSOS.API.Modules.Escalations.Domain;

public enum EmergencyEscalationReason
{
    Unknown = 0,
    NoAcknowledgement = 1,
    AllContactsDeclined = 2,
    ManualEscalation = 3,
    SimulatedEmergencyFollowUp = 4
}
