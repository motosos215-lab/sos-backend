using MotoSOS.API.Modules.Notifications.Application;
using MotoSOS.API.Modules.Notifications.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class UnconfiguredNotificationDeliveryAttemptRepository : INotificationDeliveryAttemptRepository
{
    private static InvalidOperationException CreateException() => new("MongoDB is not configured. Configure MongoDb:ConnectionString and MongoDb:DatabaseName to use Notifications API.");
    public Task<NotificationDeliveryAttempt?> GetByIdAsync(string id, CancellationToken cancellationToken) => throw CreateException();
    public Task<NotificationDeliveryAttempt?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) => throw CreateException();
    public Task<(NotificationDeliveryAttempt Attempt, bool IsDuplicate)> AddOrGetDuplicateAsync(NotificationDeliveryAttempt attempt, CancellationToken cancellationToken) => throw CreateException();
    public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByUserIdAsync(string userId, string? alertDispatchId, string? incidentId, NotificationDeliveryStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken) => throw CreateException();
    public Task<long> CountByUserIdAsync(string userId, string? alertDispatchId, string? incidentId, NotificationDeliveryStatus? status, CancellationToken cancellationToken) => throw CreateException();
    public Task UpdateAsync(NotificationDeliveryAttempt attempt, CancellationToken cancellationToken) => throw CreateException();
}
