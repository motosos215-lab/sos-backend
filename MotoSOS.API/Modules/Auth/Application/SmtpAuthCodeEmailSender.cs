using System.Net;
using System.Net.Mail;

namespace MotoSOS.API.Modules.Auth.Application;

public sealed class SmtpAuthCodeEmailSender : IAuthCodeEmailSender
{
    public async Task SendAsync(string toEmail, string subject, string body, AuthCodeEmailOptions options, CancellationToken cancellationToken)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(options.FromEmail!, options.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(toEmail);

        using var client = new SmtpClient(options.SmtpHost!, options.SmtpPort)
        {
            EnableSsl = options.UseSsl,
            Credentials = new NetworkCredential(options.SmtpUsername, options.SmtpPassword)
        };

        using CancellationTokenRegistration registration = cancellationToken.Register(client.SendAsyncCancel);
        await client.SendMailAsync(message, cancellationToken);
    }
}
