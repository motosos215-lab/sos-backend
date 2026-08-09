namespace MotoSOS.API.Modules.NotificationOutbox.Contracts;

public sealed record RetryFailedNotificationOutboxResponse(int Retried, IReadOnlyList<RetryFailedNotificationOutboxItemResponse> Items);
