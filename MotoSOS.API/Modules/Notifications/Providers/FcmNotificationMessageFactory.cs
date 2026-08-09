using Microsoft.Extensions.Options;

namespace MotoSOS.API.Modules.Notifications.Providers;

public sealed class FcmNotificationMessageFactory
{
    private readonly FcmNotificationProviderOptions _options;
    public FcmNotificationMessageFactory(IOptions<FcmNotificationProviderOptions> options) => _options = options.Value;

    public FcmPushRequest Create(NotificationProviderRequest request, string recipientToken)
    {
        return new FcmPushRequest(
            recipientToken,
            Normalize(_options.DefaultTitle) ?? "MotoSOS Alert",
            "Emergency alert available in MotoSOS.",
            new Dictionary<string, string>
            {
                ["notificationDeliveryAttemptId"] = request.NotificationDeliveryAttemptId,
                ["alertDispatchId"] = request.AlertDispatchId,
                ["incidentId"] = request.IncidentId,
                ["channel"] = request.Channel.ToString()
            },
            _options.DefaultTtlSeconds <= 0 ? 3600 : _options.DefaultTtlSeconds);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
