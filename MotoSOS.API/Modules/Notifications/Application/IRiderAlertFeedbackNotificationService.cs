using MotoSOS.API.Modules.AlertAcknowledgements.Domain;

namespace MotoSOS.API.Modules.Notifications.Application;

public interface IRiderAlertFeedbackNotificationService
{
    Task EnqueueAsync(AlertAcknowledgement acknowledgement, string feedbackEventType, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken);
}
