namespace MotoSOS.API.Modules.NotificationOutbox.Contracts;

public sealed record RetryFailedNotificationOutboxRequest(int? MaxItems);
