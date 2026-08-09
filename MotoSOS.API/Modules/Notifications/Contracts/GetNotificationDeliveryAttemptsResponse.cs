namespace MotoSOS.API.Modules.Notifications.Contracts;

public sealed record GetNotificationDeliveryAttemptsResponse(IReadOnlyList<NotificationDeliveryAttemptResponse> Attempts, int PageNumber, int PageSize, long TotalCount);
