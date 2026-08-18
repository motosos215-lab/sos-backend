using Microsoft.Extensions.Options;

namespace MotoSOS.API.Modules.Notifications.Providers;

public sealed class FcmNotificationMessageFactory
{
    private readonly FcmNotificationProviderOptions _options;
    public FcmNotificationMessageFactory(IOptions<FcmNotificationProviderOptions> options) => _options = options.Value;

    public FcmPushRequest Create(NotificationProviderRequest request, string recipientToken)
    {
        IReadOnlyDictionary<string, string> data = request.IsDirectFeedbackPush
            ? CreateFeedbackData(request)
            : new Dictionary<string, string>
            {
                ["notificationDeliveryAttemptId"] = request.NotificationDeliveryAttemptId,
                ["alertDispatchId"] = request.AlertDispatchId,
                ["incidentId"] = request.IncidentId,
                ["channel"] = request.Channel.ToString()
            };

        return new FcmPushRequest(
            recipientToken,
            Normalize(_options.DefaultTitle) ?? "MotoSOS Alert",
            "Emergency alert available in MotoSOS.",
            data,
            _options.DefaultTtlSeconds <= 0 ? 3600 : _options.DefaultTtlSeconds);
    }

    private static Dictionary<string, string> CreateFeedbackData(NotificationProviderRequest request) => new()
    {
        ["eventType"] = request.EventType!,
        ["incidentId"] = request.IncidentId,
        ["alertDispatchId"] = request.AlertDispatchId,
        ["notificationDeliveryAttemptId"] = request.NotificationDeliveryAttemptId,
        ["monitorAlertAttemptId"] = request.MonitorAlertAttemptId ?? string.Empty,
        ["monitorUserId"] = request.MonitorUserId ?? string.Empty,
        ["occurredAtUtc"] = request.OccurredAtUtc?.ToString("O") ?? string.Empty,
        ["screen"] = request.Screen ?? string.Empty
    };

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
