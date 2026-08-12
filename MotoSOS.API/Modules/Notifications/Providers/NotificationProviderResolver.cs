using Microsoft.Extensions.Options;
using MotoSOS.API.Common.Exceptions;

namespace MotoSOS.API.Modules.Notifications.Providers;

public sealed class NotificationProviderResolver : INotificationProviderResolver
{
    private readonly SimulatedNotificationProvider _simulated;
    private readonly FcmNotificationProvider? _fcm;
    private readonly EmailNotificationProvider? _email;
    private readonly SmsNotificationProvider? _sms;
    private readonly FcmNotificationProviderOptions _fcmOptions;
    private readonly EmailNotificationProviderOptions _emailOptions;
    private readonly SmsNotificationProviderOptions _smsOptions;

    public NotificationProviderResolver(SimulatedNotificationProvider simulated)
    {
        _simulated = simulated;
        _fcmOptions = new FcmNotificationProviderOptions();
        _emailOptions = new EmailNotificationProviderOptions();
        _smsOptions = new SmsNotificationProviderOptions();
    }

    public NotificationProviderResolver(SimulatedNotificationProvider simulated, FcmNotificationProvider fcm, IOptions<FcmNotificationProviderOptions> fcmOptions)
    {
        _simulated = simulated;
        _fcm = fcm;
        _fcmOptions = fcmOptions.Value;
        _emailOptions = new EmailNotificationProviderOptions();
        _smsOptions = new SmsNotificationProviderOptions();
    }

    public NotificationProviderResolver(SimulatedNotificationProvider simulated, FcmNotificationProvider fcm, EmailNotificationProvider email, IOptions<FcmNotificationProviderOptions> fcmOptions, IOptions<EmailNotificationProviderOptions> emailOptions)
    {
        _simulated = simulated;
        _fcm = fcm;
        _email = email;
        _fcmOptions = fcmOptions.Value;
        _emailOptions = emailOptions.Value;
        _smsOptions = new SmsNotificationProviderOptions();
    }

    public NotificationProviderResolver(SimulatedNotificationProvider simulated, FcmNotificationProvider fcm, EmailNotificationProvider email, SmsNotificationProvider sms, IOptions<FcmNotificationProviderOptions> fcmOptions, IOptions<EmailNotificationProviderOptions> emailOptions, IOptions<SmsNotificationProviderOptions> smsOptions)
    {
        _simulated = simulated;
        _fcm = fcm;
        _email = email;
        _sms = sms;
        _fcmOptions = fcmOptions.Value;
        _emailOptions = emailOptions.Value;
        _smsOptions = smsOptions.Value;
    }

    public INotificationProvider Resolve(NotificationProviderChannel channel) => channel switch
    {
        NotificationProviderChannel.Sms => _smsOptions.Enabled && _sms is not null ? _sms : _simulated,
        NotificationProviderChannel.Email => _emailOptions.Enabled && _email is not null ? _email : _simulated,
        NotificationProviderChannel.Push => _fcmOptions.Enabled && _fcm is not null ? _fcm : _simulated,
        _ => throw new NotificationNotAllowedAppException("Notification channel is not supported by the provider resolver.")
    };
}
