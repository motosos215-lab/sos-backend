using System.Net.Mail;
using Microsoft.Extensions.Options;
using MotoSOS.API.Modules.Auth.Domain;

namespace MotoSOS.API.Modules.Auth.Application;

public sealed class EmailAuthCodeDeliveryProvider : IAuthCodeDeliveryProvider
{
    private readonly IAuthCodeEmailSender _sender;
    private readonly AuthCodeOptions _options;
    private readonly ILogger<EmailAuthCodeDeliveryProvider> _logger;

    public EmailAuthCodeDeliveryProvider(IAuthCodeEmailSender sender, IOptions<AuthCodeOptions> options, ILogger<EmailAuthCodeDeliveryProvider> logger)
    {
        _sender = sender;
        _options = options.Value;
        _logger = logger;
    }

    public AuthCodeDeliveryChannel Channel => AuthCodeDeliveryChannel.Email;

    public async Task<AuthCodeDeliveryStatus> DeliverAsync(string emailNormalized, AuthCodePurpose purpose, string code, CancellationToken cancellationToken)
    {
        try
        {
            await _sender.SendAsync(emailNormalized, GetSubject(purpose), GetBody(code), _options.Email, cancellationToken);
            _logger.LogInformation("Auth code email delivery completed.");
            return AuthCodeDeliveryStatus.Delivered;
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or FormatException)
        {
            _logger.LogWarning("Auth code email delivery failed.");
            return AuthCodeDeliveryStatus.Failed;
        }
    }

    private static string GetSubject(AuthCodePurpose purpose) => purpose switch
    {
        AuthCodePurpose.PasswordReset => "MotoSOS - Código para restablecer contraseña",
        AuthCodePurpose.AccessLogin => "MotoSOS - Código de acceso",
        _ => "MotoSOS - Código de seguridad"
    };

    private string GetBody(string code) => $"MotoSOS\n\nTu código es: {code}\n\nEste código vence en {_options.TtlMinutes} minutos.\n\nSi no solicitaste este código, ignora este mensaje.";
}
