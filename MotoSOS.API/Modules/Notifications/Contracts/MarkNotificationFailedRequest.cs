namespace MotoSOS.API.Modules.Notifications.Contracts;

public sealed record MarkNotificationFailedRequest(string? FailureReason, string? Notes);
