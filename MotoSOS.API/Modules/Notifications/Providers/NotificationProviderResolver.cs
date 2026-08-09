using MotoSOS.API.Common.Exceptions;

namespace MotoSOS.API.Modules.Notifications.Providers;

public sealed class NotificationProviderResolver : INotificationProviderResolver
{
    private readonly SimulatedNotificationProvider _simulated;

    public NotificationProviderResolver(SimulatedNotificationProvider simulated)
    {
        _simulated = simulated;
    }

    public INotificationProvider Resolve(NotificationProviderChannel channel) => channel switch
    {
        NotificationProviderChannel.Sms => _simulated,
        NotificationProviderChannel.Email => _simulated,
        NotificationProviderChannel.Push => _simulated,
        _ => throw new NotificationNotAllowedAppException("Notification channel is not supported by the provider resolver.")
    };
}
