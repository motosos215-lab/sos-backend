namespace MotoSOS.API.Modules.SosAlerts.Contracts;

public sealed record SosAlertDispatchResponse(string Id, string IncidentId, string Status, int ContactsCount);
