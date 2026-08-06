namespace MotoSOS.API.Modules.Notifications.Contracts;

public sealed record PrepareNotificationAttemptsRequest(string? AlertDispatchId, string? Notes);
