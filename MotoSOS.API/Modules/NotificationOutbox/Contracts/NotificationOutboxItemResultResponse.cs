namespace MotoSOS.API.Modules.NotificationOutbox.Contracts;

public sealed record NotificationOutboxItemResultResponse(string NotificationDeliveryAttemptId, string Status, string Channel, string? Reason);
