using MongoDB.Driver;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Collections;
using MotoSOS.API.Modules.AlertDispatch.Application;
using MotoSOS.API.Modules.AlertDispatch.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class MongoAlertDispatchRepository : IAlertDispatchRepository
{
    private readonly IMongoCollection<AlertDispatchRequest> _alertDispatches;

    public MongoAlertDispatchRepository(IMongoDatabase database) => _alertDispatches = database.GetCollection<AlertDispatchRequest>(MongoCollectionNames.AlertDispatchRequests);

    public async Task<AlertDispatchRequest?> GetByIdAsync(string id, CancellationToken cancellationToken) => await _alertDispatches.Find(alert => alert.Id == id).FirstOrDefaultAsync(cancellationToken);
    public async Task<AlertDispatchRequest?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) => await _alertDispatches.Find(alert => alert.IdempotencyKey == idempotencyKey).FirstOrDefaultAsync(cancellationToken);

    public async Task<(AlertDispatchRequest AlertDispatch, bool IsDuplicate)> AddOrGetDuplicateAsync(AlertDispatchRequest alertDispatch, CancellationToken cancellationToken)
    {
        AlertDispatchRequest? existing = await GetByIdempotencyKeyAsync(alertDispatch.IdempotencyKey, cancellationToken);
        if (existing is not null) return (existing, true);
        try
        {
            await _alertDispatches.InsertOneAsync(alertDispatch, cancellationToken: cancellationToken);
            return (alertDispatch, false);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            existing = await GetByIdempotencyKeyAsync(alertDispatch.IdempotencyKey, cancellationToken);
            if (existing is not null) return (existing, true);
            throw;
        }
    }

    public async Task<IReadOnlyList<AlertDispatchRequest>> ListByIncidentIdAsync(string userId, string incidentId, CancellationToken cancellationToken) =>
        await _alertDispatches.Find(alert => alert.UserId == userId && alert.IncidentId == incidentId).SortByDescending(alert => alert.CreatedAtUtc).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AlertDispatchRequest>> ListByUserIdAsync(string userId, AlertDispatchStatus? status, string? incidentId, int pageNumber, int pageSize, CancellationToken cancellationToken) =>
        await _alertDispatches.Find(BuildUserFilter(userId, status, incidentId)).SortByDescending(a => a.CreatedAtUtc).Skip((pageNumber - 1) * pageSize).Limit(pageSize).ToListAsync(cancellationToken);

    public async Task<long> CountByUserIdAsync(string userId, AlertDispatchStatus? status, string? incidentId, CancellationToken cancellationToken) =>
        await _alertDispatches.CountDocumentsAsync(BuildUserFilter(userId, status, incidentId), cancellationToken: cancellationToken);

    public async Task UpdateAsync(AlertDispatchRequest alertDispatch, CancellationToken cancellationToken) => await _alertDispatches.ReplaceOneAsync(existing => existing.Id == alertDispatch.Id, alertDispatch, cancellationToken: cancellationToken);

    private static FilterDefinition<AlertDispatchRequest> BuildUserFilter(string userId, AlertDispatchStatus? status, string? incidentId)
    {
        FilterDefinitionBuilder<AlertDispatchRequest> builder = Builders<AlertDispatchRequest>.Filter;
        FilterDefinition<AlertDispatchRequest> filter = builder.Eq(alert => alert.UserId, userId);
        if (status.HasValue) filter &= builder.Eq(alert => alert.Status, status.Value);
        if (!string.IsNullOrWhiteSpace(incidentId)) filter &= builder.Eq(alert => alert.IncidentId, incidentId);
        return filter;
    }
}
