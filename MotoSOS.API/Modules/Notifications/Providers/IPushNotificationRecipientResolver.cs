namespace MotoSOS.API.Modules.Notifications.Providers;

public interface IPushNotificationRecipientResolver
{
    Task<PushNotificationRecipientResolution> ResolveAsync(string notificationDeliveryAttemptId, CancellationToken cancellationToken);
}
