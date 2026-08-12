namespace MotoSOS.API.Modules.Notifications.Providers;

public interface ISmsNotificationSender
{
    Task<string?> SendAsync(SmsNotificationMessage message, SmsNotificationProviderOptions options, CancellationToken cancellationToken);
}
