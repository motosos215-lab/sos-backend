using System.Net.Http;
using Microsoft.Extensions.Options;
using MotoSOS.API.Common.Abstractions;

namespace MotoSOS.API.Modules.Notifications.Providers;

public sealed class SmsNotificationProvider : INotificationProvider
{
    private readonly ISmsNotificationSender _sender;
    private readonly SmsNotificationProviderOptionsValidator _validator;
    private readonly SmsNotificationProviderOptions _options;
    private readonly IClock _clock;

    public SmsNotificationProvider(ISmsNotificationSender sender, SmsNotificationProviderOptionsValidator validator, IOptions<SmsNotificationProviderOptions> options, IClock clock)
    {
        _sender = sender;
        _validator = validator;
        _options = options.Value;
        _clock = clock;
    }

    public NotificationProviderType ProviderType => NotificationProviderType.Sms;

    public async Task<NotificationProviderResult> SendAsync(NotificationProviderRequest request, CancellationToken cancellationToken)
    {
        DateTimeOffset now = _clock.UtcNow;
        SmsProviderConfigurationStatus config = _validator.Validate(_options);
        if (!config.Configured) return Failed(request, "provider_not_configured", now);
        if (request.SimulateFailure) return Failed(request, "simulated_failure_requested", now);
        if (!TryNormalizePhoneNumber(request.RecipientSmsAddress, _options.DefaultCountryCode, out string? phoneNumber) || phoneNumber is null) return Failed(request, "sms_recipient_not_available", now);

        try
        {
            string body = $"MotoSOS: alerta de emergencia detectada. Abre la app MotoSOS para revisar detalles. ID: {request.NotificationDeliveryAttemptId}";
            string? messageId = await _sender.SendAsync(new SmsNotificationMessage(phoneNumber, body, _options.Sender), _options, cancellationToken);
            return new NotificationProviderResult(ProviderType, request.Channel, NotificationProviderDeliveryStatus.Sent, string.IsNullOrWhiteSpace(messageId) ? $"sms-{request.NotificationDeliveryAttemptId}" : messageId, "sms-sent", null, null, now, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            return Failed(request, "sms_provider_failed", now);
        }
    }

    public static bool TryNormalizePhoneNumber(string? phoneNumber, string? defaultCountryCode, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(phoneNumber)) return false;

        string cleaned = phoneNumber.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal).Replace("(", string.Empty, StringComparison.Ordinal).Replace(")", string.Empty, StringComparison.Ordinal);
        if (cleaned.Length == 0) return false;
        if (!cleaned.StartsWith('+'))
        {
            if (string.IsNullOrWhiteSpace(defaultCountryCode)) return false;
            cleaned = defaultCountryCode.Trim() + cleaned;
        }

        if (cleaned.Length < 8 || cleaned.Length > 16 || cleaned.Count(c => c == '+') != 1 || cleaned[0] != '+' || cleaned.Skip(1).Any(c => !char.IsDigit(c))) return false;
        normalized = cleaned;
        return true;
    }

    private NotificationProviderResult Failed(NotificationProviderRequest request, string failureCode, DateTimeOffset now) =>
        new(ProviderType, request.Channel, NotificationProviderDeliveryStatus.Failed, null, "sms-failed", SanitizeFailureCode(failureCode), "SMS notification failed in a controlled way.", null, now);

    private static string SanitizeFailureCode(string? code) => string.IsNullOrWhiteSpace(code) ? "sms_provider_failed" : code.Trim().ToLowerInvariant().Replace(' ', '_')[..Math.Min(code.Trim().Length, 100)];
}
