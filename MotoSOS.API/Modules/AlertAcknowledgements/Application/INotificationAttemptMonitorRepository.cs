using MotoSOS.API.Modules.Notifications.Domain;

namespace MotoSOS.API.Modules.AlertAcknowledgements.Application;

public interface INotificationAttemptMonitorRepository
{
    Task<NotificationDeliveryAttempt?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByEmergencyContactIdsAsync(IReadOnlyCollection<string> emergencyContactIds, int pageNumber, int pageSize, CancellationToken cancellationToken);
    Task<long> CountByEmergencyContactIdsAsync(IReadOnlyCollection<string> emergencyContactIds, CancellationToken cancellationToken);
}
