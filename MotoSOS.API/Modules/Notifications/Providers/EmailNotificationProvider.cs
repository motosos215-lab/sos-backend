using System.Net.Mail;
using Microsoft.Extensions.Options;
using MotoSOS.API.Common.Abstractions;

namespace MotoSOS.API.Modules.Notifications.Providers;

public sealed class EmailNotificationProvider : INotificationProvider
{
    private const string Subject = "MotoSOS - Alerta de emergencia";
    private readonly IEmailNotificationSender _sender;
    private readonly EmailNotificationProviderOptionsValidator _validator;
    private readonly EmailNotificationProviderOptions _options;
    private readonly IClock _clock;

    public EmailNotificationProvider(IEmailNotificationSender sender, EmailNotificationProviderOptionsValidator validator, IOptions<EmailNotificationProviderOptions> options, IClock clock)
    {
        _sender = sender;
        _validator = validator;
        _options = options.Value;
        _clock = clock;
    }

    public NotificationProviderType ProviderType => NotificationProviderType.Email;

    public async Task<NotificationProviderResult> SendAsync(NotificationProviderRequest request, CancellationToken cancellationToken)
    {
        DateTimeOffset now = _clock.UtcNow;
        EmailProviderConfigurationStatus config = _validator.Validate(_options);
        if (!config.Configured) return Failed(request, "provider_not_configured", now);
        if (request.SimulateFailure) return Failed(request, "simulated_failure_requested", now);
        if (string.IsNullOrWhiteSpace(request.RecipientEmail)) return Failed(request, "email_recipient_not_available", now);

        try
        {
            string? messageId = await _sender.SendAsync(new EmailNotificationMessage(request.RecipientEmail, Subject, BuildBody(request)), _options, cancellationToken);
            return new NotificationProviderResult(ProviderType, request.Channel, NotificationProviderDeliveryStatus.Sent, string.IsNullOrWhiteSpace(messageId) ? $"email-{request.NotificationDeliveryAttemptId}" : messageId, "email-sent", null, null, now, null);
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or FormatException)
        {
            return Failed(request, "email_provider_failed", now);
        }
    }

    private static string BuildBody(NotificationProviderRequest request) =>
        $"MotoSOS detecto una alerta de emergencia.{Environment.NewLine}{Environment.NewLine}" +
        $"Abre la app MotoSOS para revisar los detalles y responder a la alerta.{Environment.NewLine}{Environment.NewLine}" +
        $"incidentId: {request.IncidentId}{Environment.NewLine}" +
        $"alertDispatchId: {request.AlertDispatchId}{Environment.NewLine}" +
        $"notificationDeliveryAttemptId: {request.NotificationDeliveryAttemptId}{Environment.NewLine}" +
        "canal: Email";

    private NotificationProviderResult Failed(NotificationProviderRequest request, string failureCode, DateTimeOffset now) =>
        new(ProviderType, request.Channel, NotificationProviderDeliveryStatus.Failed, null, "email-failed", SanitizeFailureCode(failureCode), "Email notification failed in a controlled way.", null, now);

    private static string SanitizeFailureCode(string? code) => string.IsNullOrWhiteSpace(code) ? "email_provider_failed" : code.Trim().ToLowerInvariant().Replace(' ', '_')[..Math.Min(code.Trim().Length, 100)];
}
