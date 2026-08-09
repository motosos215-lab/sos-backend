using Microsoft.Extensions.Options;
using MotoSOS.API.Common.Exceptions;

namespace MotoSOS.API.Modules.Notifications.Providers;

public sealed class NotificationProviderResolver : INotificationProviderResolver
{
    private readonly SimulatedNotificationProvider _simulated;
    private readonly FcmNotificationProvider? _fcm;
    private readonly FcmNotificationProviderOptions _fcmOptions;

    public NotificationProviderResolver(SimulatedNotificationProvider simulated)
    {
        _simulated = simulated;
        _fcmOptions = new FcmNotificationProviderOptions();
    }

    public NotificationProviderResolver(SimulatedNotificationProvider simulated, FcmNotificationProvider fcm, IOptions<FcmNotificationProviderOptions> fcmOptions)
    {
        _simulated = simulated;
        _fcm = fcm;
        _fcmOptions = fcmOptions.Value;
    }

    public INotificationProvider Resolve(NotificationProviderChannel channel) => channel switch
    {
        NotificationProviderChannel.Sms => _simulated,
        NotificationProviderChannel.Email => _simulated,
        NotificationProviderChannel.Push => _fcmOptions.Enabled && _fcm is not null ? _fcm : _simulated,
        _ => throw new NotificationNotAllowedAppException("Notification channel is not supported by the provider resolver.")
    };
}
