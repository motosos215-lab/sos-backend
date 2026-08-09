namespace MotoSOS.API.Modules.EmergencyStatus.Contracts;

public sealed record EmergencyAlertDispatchStatusResponse(string Id, string Status, string Priority, string Reason, DateTimeOffset CreatedAtUtc);
