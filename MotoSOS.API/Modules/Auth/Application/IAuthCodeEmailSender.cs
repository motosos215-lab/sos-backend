namespace MotoSOS.API.Modules.Auth.Application;

public interface IAuthCodeEmailSender
{
    Task SendAsync(string toEmail, string subject, string body, AuthCodeEmailOptions options, CancellationToken cancellationToken);
}
