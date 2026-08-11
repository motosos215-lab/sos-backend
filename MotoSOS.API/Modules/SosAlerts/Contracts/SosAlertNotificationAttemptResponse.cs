namespace MotoSOS.API.Modules.SosAlerts.Contracts;

public sealed record SosAlertNotificationAttemptResponse(string Id, string Channel, string Status, string Provider, string EmergencyContactId, string? ContactFullName);
