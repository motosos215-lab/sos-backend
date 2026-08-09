namespace MotoSOS.API.Modules.Incidents.Domain;

public enum IncidentCause
{
    CountdownTimeout = 1,
    UserRequestedHelp = 2,
    CriticalEvent = 3,
    ManualSos = 4,
    Unknown = 5
}
