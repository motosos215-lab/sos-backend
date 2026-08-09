namespace MotoSOS.API.Modules.NotificationOutbox.Contracts;

public sealed record RetryFailedNotificationOutboxItemResponse(string NotificationDeliveryAttemptId, string Status);
