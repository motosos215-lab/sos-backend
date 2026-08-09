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
    Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByStatusAsync(NotificationDeliveryStatus status, int maxItems, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>([]);
    Task<NotificationDeliveryAttempt?> TryMarkSimulatedSentAsync(string attemptId, DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult<NotificationDeliveryAttempt?>(null);
    Task<NotificationDeliveryAttempt?> TryMarkSimulatedSentAsync(string attemptId, string? providerMessageId, DateTimeOffset now, CancellationToken cancellationToken) => TryMarkSimulatedSentAsync(attemptId, now, cancellationToken);
    Task<NotificationDeliveryAttempt?> TryMarkSentAsync(string attemptId, NotificationProvider provider, string? providerMessageId, DateTimeOffset sentAtUtc, CancellationToken cancellationToken) => TryMarkSimulatedSentAsync(attemptId, providerMessageId, sentAtUtc, cancellationToken);
    Task<NotificationDeliveryAttempt?> TryMarkFailedAsync(string attemptId, string failureReason, DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult<NotificationDeliveryAttempt?>(null);
    Task<NotificationDeliveryAttempt?> TryMarkFailedAsync(string attemptId, NotificationProvider provider, string failureReason, DateTimeOffset now, CancellationToken cancellationToken) => TryMarkFailedAsync(attemptId, failureReason, now, cancellationToken);
    Task<NotificationDeliveryAttempt?> TryResetFailedToPreparedAsync(string attemptId, DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult<NotificationDeliveryAttempt?>(null);
    Task<long> CountByStatusAsync(NotificationDeliveryStatus status, CancellationToken cancellationToken) => Task.FromResult(0L);
    Task UpdateAsync(NotificationDeliveryAttempt attempt, CancellationToken cancellationToken);
}
