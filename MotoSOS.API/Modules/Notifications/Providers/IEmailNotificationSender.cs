namespace MotoSOS.API.Modules.Notifications.Providers;

public interface IEmailNotificationSender
{
    Task<string?> SendAsync(EmailNotificationMessage message, EmailNotificationProviderOptions options, CancellationToken cancellationToken);
}
