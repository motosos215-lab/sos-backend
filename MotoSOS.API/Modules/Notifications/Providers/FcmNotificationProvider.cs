using Microsoft.Extensions.Options;
using MotoSOS.API.Common.Abstractions;

namespace MotoSOS.API.Modules.Notifications.Providers;

public sealed class FcmNotificationProvider : INotificationProvider
{
    private readonly IFcmPushClient _client;
    private readonly IPushNotificationRecipientResolver _recipients;
    private readonly FcmNotificationMessageFactory _messages;
    private readonly FcmNotificationProviderOptionsValidator _validator;
    private readonly FcmNotificationProviderOptions _options;
    private readonly IClock _clock;

    public FcmNotificationProvider(IFcmPushClient client, IPushNotificationRecipientResolver recipients, FcmNotificationMessageFactory messages, FcmNotificationProviderOptionsValidator validator, IOptions<FcmNotificationProviderOptions> options, IClock clock)
    {
        _client = client;
        _recipients = recipients;
        _messages = messages;
        _validator = validator;
        _options = options.Value;
        _clock = clock;
    }

    public NotificationProviderType ProviderType => NotificationProviderType.Fcm;

    public async Task<NotificationProviderResult> SendAsync(NotificationProviderRequest request, CancellationToken cancellationToken)
    {
        DateTimeOffset now = _clock.UtcNow;
        FcmProviderConfigurationStatus config = _validator.Validate(_options);
        if (!config.Configured) return Failed(request, "provider_not_configured", now);
        if (request.SimulateFailure) return Failed(request, "simulated_failure_requested", now);

        PushNotificationRecipientResolution resolution = await _recipients.ResolveAsync(request.NotificationDeliveryAttemptId, cancellationToken);
        if (resolution.Recipient is null) return Failed(request, resolution.FailureCode ?? "push_recipient_not_available", now);

        FcmPushResult result = await _client.SendAsync(_messages.Create(request, resolution.Recipient.Token.TokenValue), cancellationToken);
        if (!result.Success) return Failed(request, SanitizeFailureCode(result.FailureCode), now);

        return new NotificationProviderResult(ProviderType, request.Channel, NotificationProviderDeliveryStatus.Sent, result.MessageId, "fcm-sent", null, null, now, null);
    }

    private NotificationProviderResult Failed(NotificationProviderRequest request, string failureCode, DateTimeOffset now) =>
        new(ProviderType, request.Channel, NotificationProviderDeliveryStatus.Failed, null, "fcm-failed", SanitizeFailureCode(failureCode), "FCM notification failed in a controlled way.", null, now);

    private static string SanitizeFailureCode(string? code) => string.IsNullOrWhiteSpace(code) ? "fcm_provider_failed" : code.Trim().ToLowerInvariant().Replace(' ', '_')[..Math.Min(code.Trim().Length, 100)];
}
