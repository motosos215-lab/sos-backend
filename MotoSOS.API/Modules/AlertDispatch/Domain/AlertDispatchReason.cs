namespace MotoSOS.API.Modules.AlertDispatch.Domain;

public enum AlertDispatchReason
{
    IncidentCreated = 1,
    ManualSos = 2,
    CountdownTimeout = 3,
    CriticalEvent = 4,
    UserRequestedHelp = 5,
    Unknown = 6
}
