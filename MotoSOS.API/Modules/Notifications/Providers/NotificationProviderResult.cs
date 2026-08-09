namespace MotoSOS.API.Modules.Notifications.Providers;

public sealed record NotificationProviderResult(NotificationProviderType ProviderType, NotificationProviderChannel Channel, NotificationProviderDeliveryStatus DeliveryStatus, string? ProviderMessageId, string? ProviderStatusCode, string? ErrorCode, string? ErrorMessage, DateTimeOffset? SentAtUtc, DateTimeOffset? FailedAtUtc);
