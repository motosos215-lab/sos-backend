namespace MotoSOS.API.Modules.AlertAcknowledgements.Application;

public interface IAlertAcknowledgementIdempotencyKeyFactory
{
    string Create(string monitorUserId, string notificationDeliveryAttemptId);
}
