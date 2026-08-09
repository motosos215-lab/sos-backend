namespace MotoSOS.API.Modules.Notifications.Providers;

public interface INotificationProviderResolver
{
    INotificationProvider Resolve(NotificationProviderChannel channel);
}
