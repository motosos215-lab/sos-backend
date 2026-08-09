namespace MotoSOS.API.Modules.Notifications.Contracts;

public sealed record PrepareNotificationAttemptsResponse(IReadOnlyList<NotificationDeliveryAttemptResponse> Attempts);
