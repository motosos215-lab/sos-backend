using MongoDB.Driver;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Collections;
using MotoSOS.API.Modules.AlertAcknowledgements.Application;
using MotoSOS.API.Modules.Notifications.Application;
using MotoSOS.API.Modules.Notifications.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class MongoNotificationDeliveryAttemptRepository : INotificationDeliveryAttemptRepository, INotificationAttemptMonitorRepository
{
    private readonly IMongoCollection<NotificationDeliveryAttempt> _attempts;
    public MongoNotificationDeliveryAttemptRepository(IMongoDatabase database) => _attempts = database.GetCollection<NotificationDeliveryAttempt>(MongoCollectionNames.NotificationDeliveryAttempts);
    public async Task<NotificationDeliveryAttempt?> GetByIdAsync(string id, CancellationToken cancellationToken) => await _attempts.Find(a => a.Id == id).FirstOrDefaultAsync(cancellationToken);
    public async Task<NotificationDeliveryAttempt?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) => await _attempts.Find(a => a.IdempotencyKey == idempotencyKey).FirstOrDefaultAsync(cancellationToken);
    public async Task<(NotificationDeliveryAttempt Attempt, bool IsDuplicate)> AddOrGetDuplicateAsync(NotificationDeliveryAttempt attempt, CancellationToken cancellationToken)
    {
        NotificationDeliveryAttempt? existing = await GetByIdempotencyKeyAsync(attempt.IdempotencyKey, cancellationToken);
        if (existing is not null) return (existing, true);
        try { await _attempts.InsertOneAsync(attempt, cancellationToken: cancellationToken); return (attempt, false); }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            existing = await GetByIdempotencyKeyAsync(attempt.IdempotencyKey, cancellationToken);
            if (existing is not null) return (existing, true);
            throw;
        }
    }
    public async Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByIncidentIdAsync(string userId, string incidentId, CancellationToken cancellationToken) =>
        await _attempts.Find(a => a.UserId == userId && a.IncidentId == incidentId).SortByDescending(a => a.CreatedAtUtc).ToListAsync(cancellationToken);
    public async Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByAlertDispatchIdAsync(string userId, string alertDispatchId, CancellationToken cancellationToken) =>
        await _attempts.Find(a => a.UserId == userId && a.AlertDispatchId == alertDispatchId).SortByDescending(a => a.CreatedAtUtc).ToListAsync(cancellationToken);
    public async Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByUserIdAsync(string userId, string? alertDispatchId, string? incidentId, NotificationDeliveryStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken) =>
        await _attempts.Find(BuildFilter(userId, alertDispatchId, incidentId, status)).SortByDescending(a => a.CreatedAtUtc).Skip((pageNumber - 1) * pageSize).Limit(pageSize).ToListAsync(cancellationToken);
    public async Task<long> CountByUserIdAsync(string userId, string? alertDispatchId, string? incidentId, NotificationDeliveryStatus? status, CancellationToken cancellationToken) =>
        await _attempts.CountDocumentsAsync(BuildFilter(userId, alertDispatchId, incidentId, status), cancellationToken: cancellationToken);
    public async Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByEmergencyContactIdsAsync(IReadOnlyCollection<string> emergencyContactIds, int pageNumber, int pageSize, CancellationToken cancellationToken) =>
        await _attempts.Find(Builders<NotificationDeliveryAttempt>.Filter.In(a => a.EmergencyContactId, emergencyContactIds)).SortByDescending(a => a.CreatedAtUtc).Skip((pageNumber - 1) * pageSize).Limit(pageSize).ToListAsync(cancellationToken);
    public async Task<long> CountByEmergencyContactIdsAsync(IReadOnlyCollection<string> emergencyContactIds, CancellationToken cancellationToken) =>
        await _attempts.CountDocumentsAsync(Builders<NotificationDeliveryAttempt>.Filter.In(a => a.EmergencyContactId, emergencyContactIds), cancellationToken: cancellationToken);
    public async Task UpdateAsync(NotificationDeliveryAttempt attempt, CancellationToken cancellationToken) => await _attempts.ReplaceOneAsync(existing => existing.Id == attempt.Id, attempt, cancellationToken: cancellationToken);
    private static FilterDefinition<NotificationDeliveryAttempt> BuildFilter(string userId, string? alertDispatchId, string? incidentId, NotificationDeliveryStatus? status)
    {
        FilterDefinitionBuilder<NotificationDeliveryAttempt> builder = Builders<NotificationDeliveryAttempt>.Filter;
        FilterDefinition<NotificationDeliveryAttempt> filter = builder.Eq(a => a.UserId, userId);
        if (!string.IsNullOrWhiteSpace(alertDispatchId)) filter &= builder.Eq(a => a.AlertDispatchId, alertDispatchId);
        if (!string.IsNullOrWhiteSpace(incidentId)) filter &= builder.Eq(a => a.IncidentId, incidentId);
        if (status.HasValue) filter &= builder.Eq(a => a.Status, status.Value);
        return filter;
    }
}
