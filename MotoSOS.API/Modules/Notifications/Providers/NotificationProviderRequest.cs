using MotoSOS.API.Modules.Notifications.Domain;

namespace MotoSOS.API.Modules.Notifications.Providers;

public sealed record NotificationProviderRequest(
    string NotificationDeliveryAttemptId,
    string AlertDispatchId,
    string IncidentId,
    NotificationProviderChannel Channel,
    bool SimulateFailure,
    string? RecipientEmail = null,
    string? RecipientSmsAddress = null,
    string? RecipientUserId = null,
    string? EventType = null,
    string? MonitorAlertAttemptId = null,
    string? MonitorUserId = null,
    string? Screen = null,
    DateTimeOffset? OccurredAtUtc = null)
{
    public bool IsDirectFeedbackPush => Channel == NotificationProviderChannel.Push && !string.IsNullOrWhiteSpace(EventType) && !string.IsNullOrWhiteSpace(RecipientUserId);
}
