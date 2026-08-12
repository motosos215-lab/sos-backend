using System.Net;
using System.Net.Mail;

namespace MotoSOS.API.Modules.Notifications.Providers;

public sealed class SmtpEmailNotificationSender : IEmailNotificationSender
{
    public async Task<string?> SendAsync(EmailNotificationMessage message, EmailNotificationProviderOptions options, CancellationToken cancellationToken)
    {
        using var mail = new MailMessage
        {
            From = new MailAddress(options.FromEmail!, options.FromName),
            Subject = message.Subject,
            Body = message.Body,
            IsBodyHtml = false
        };
        mail.To.Add(message.ToEmail);

        using var client = new SmtpClient(options.SmtpHost!, options.SmtpPort)
        {
            EnableSsl = options.UseSsl,
            Credentials = new NetworkCredential(options.SmtpUsername, options.SmtpPassword)
        };

        using CancellationTokenRegistration registration = cancellationToken.Register(client.SendAsyncCancel);
        await client.SendMailAsync(mail, cancellationToken);
        return null;
    }
}
