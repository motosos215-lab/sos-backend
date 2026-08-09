namespace MotoSOS.API.Modules.Notifications.Providers;

public interface INotificationProvider
{
    NotificationProviderType ProviderType { get; }
    Task<NotificationProviderResult> SendAsync(NotificationProviderRequest request, CancellationToken cancellationToken);
}
