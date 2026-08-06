using MotoSOS.API.Modules.AlertAcknowledgements.Application;
using MotoSOS.API.Modules.Notifications.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class UnconfiguredNotificationAttemptMonitorRepository : INotificationAttemptMonitorRepository
{
    public Task<NotificationDeliveryAttempt?> GetByIdAsync(string id, CancellationToken cancellationToken) => throw new InvalidOperationException("MongoDB is not configured. Configure MongoDb:ConnectionString and MongoDb:DatabaseName to use Alert Acknowledgements API.");
    public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByEmergencyContactIdsAsync(IReadOnlyCollection<string> emergencyContactIds, int pageNumber, int pageSize, CancellationToken cancellationToken) => throw new InvalidOperationException("MongoDB is not configured. Configure MongoDb:ConnectionString and MongoDb:DatabaseName to use Alert Acknowledgements API.");
    public Task<long> CountByEmergencyContactIdsAsync(IReadOnlyCollection<string> emergencyContactIds, CancellationToken cancellationToken) => throw new InvalidOperationException("MongoDB is not configured. Configure MongoDb:ConnectionString and MongoDb:DatabaseName to use Alert Acknowledgements API.");
}
