using MotoSOS.API.Modules.Notifications.Domain;

namespace MotoSOS.API.Modules.Notifications.Application;

public interface INotificationIdempotencyKeyFactory
{
    string Create(string userId, string alertDispatchId, string emergencyContactId, NotificationChannel channel, int attemptNumber);
}
