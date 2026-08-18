namespace MotoSOS.API.Modules.Notifications.Application;

public interface IRiderAlertFeedbackNotificationIdempotencyKeyFactory
{
    string Create(string riderUserId, string incidentId, string monitorNotificationDeliveryAttemptId, string feedbackEventType);
}
