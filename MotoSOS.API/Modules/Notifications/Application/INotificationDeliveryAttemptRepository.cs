using MotoSOS.API.Modules.Notifications.Domain;

namespace MotoSOS.API.Modules.Notifications.Application;

public interface INotificationDeliveryAttemptRepository
{
    Task<NotificationDeliveryAttempt?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task<NotificationDeliveryAttempt?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);
    Task<(NotificationDeliveryAttempt Attempt, bool IsDuplicate)> AddOrGetDuplicateAsync(NotificationDeliveryAttempt attempt, CancellationToken cancellationToken);
    Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByIncidentIdAsync(string userId, string incidentId, CancellationToken cancellationToken) => ListByUserIdAsync(userId, null, incidentId, null, 1, int.MaxValue, cancellationToken);
    Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByAlertDispatchIdAsync(string userId, string alertDispatchId, CancellationToken cancellationToken) => ListByUserIdAsync(userId, alertDispatchId, null, null, 1, int.MaxValue, cancellationToken);
    Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByUserIdAsync(string userId, string? alertDispatchId, string? incidentId, NotificationDeliveryStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken);
    Task<long> CountByUserIdAsync(string userId, string? alertDispatchId, string? incidentId, NotificationDeliveryStatus? status, CancellationToken cancellationToken);
    Task UpdateAsync(NotificationDeliveryAttempt attempt, CancellationToken cancellationToken);
}
