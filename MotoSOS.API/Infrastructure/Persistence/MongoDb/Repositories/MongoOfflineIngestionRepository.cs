using MongoDB.Driver;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Collections;
using MotoSOS.API.Modules.OfflineIngestion.Application;
using MotoSOS.API.Modules.OfflineIngestion.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class MongoOfflineIngestionRepository : IOfflineIngestionRepository
{
    private readonly IMongoCollection<OfflineIngestionRecord> _records;

    public MongoOfflineIngestionRepository(IMongoDatabase database)
    {
        _records = database.GetCollection<OfflineIngestionRecord>(MongoCollectionNames.OfflineIngestionRecords);
    }

    public async Task<OfflineIngestionRecord?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) =>
        await _records.Find(record => record.IdempotencyKey == idempotencyKey).FirstOrDefaultAsync(cancellationToken);

    public async Task<(OfflineIngestionRecord Record, bool IsDuplicate)> AddOrGetDuplicateAsync(OfflineIngestionRecord record, CancellationToken cancellationToken)
    {
        OfflineIngestionRecord? existing = await GetByIdempotencyKeyAsync(record.IdempotencyKey, cancellationToken);
        if (existing is not null) return (existing, true);

        try
        {
            await _records.InsertOneAsync(record, cancellationToken: cancellationToken);
            return (record, false);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            existing = await GetByIdempotencyKeyAsync(record.IdempotencyKey, cancellationToken);
            if (existing is not null) return (existing, true);
            throw;
        }
    }

    public async Task<IReadOnlyList<OfflineIngestionRecord>> ListPendingByUserIdAsync(string userId, int maxItems, CancellationToken cancellationToken) =>
        await _records.Find(record => record.UserId == userId && record.ProcessingStatus == OfflineIngestionProcessingStatus.PendingProcessing)
            .SortBy(record => record.ReceivedAtUtc)
            .Limit(maxItems)
            .ToListAsync(cancellationToken);

    public async Task<OfflineIngestionRecord?> TryMarkProcessingAsync(string id, string userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        FilterDefinition<OfflineIngestionRecord> filter = Builders<OfflineIngestionRecord>.Filter.Where(record => record.Id == id && record.UserId == userId && record.ProcessingStatus == OfflineIngestionProcessingStatus.PendingProcessing);
        UpdateDefinition<OfflineIngestionRecord> update = Builders<OfflineIngestionRecord>.Update
            .Set(record => record.ProcessingStatus, OfflineIngestionProcessingStatus.Processing)
            .Set(record => record.ProcessingStartedAtUtc, now)
            .Set(record => record.UpdatedAtUtc, now);
        return await _records.FindOneAndUpdateAsync(filter, update, new FindOneAndUpdateOptions<OfflineIngestionRecord> { ReturnDocument = ReturnDocument.After }, cancellationToken);
    }

    public async Task MarkProcessedAsync(string id, string userId, string remoteRecordId, DateTimeOffset now, CancellationToken cancellationToken) =>
        await UpdateProcessingStatusAsync(id, userId, OfflineIngestionProcessingStatus.Processed, now, Builders<OfflineIngestionRecord>.Update.Set(record => record.RemoteRecordId, remoteRecordId).Set(record => record.ProcessedAtUtc, now), cancellationToken);

    public async Task MarkIgnoredAsync(string id, string userId, string reason, DateTimeOffset now, CancellationToken cancellationToken) =>
        await UpdateProcessingStatusAsync(id, userId, OfflineIngestionProcessingStatus.Ignored, now, Builders<OfflineIngestionRecord>.Update.Set(record => record.ProcessingReason, reason).Set(record => record.ProcessedAtUtc, now), cancellationToken);

    public async Task MarkFailedPermanentAsync(string id, string userId, string errorCode, string errorMessage, DateTimeOffset now, CancellationToken cancellationToken) =>
        await UpdateProcessingStatusAsync(id, userId, OfflineIngestionProcessingStatus.FailedPermanent, now, Builders<OfflineIngestionRecord>.Update.Set(record => record.ProcessingErrorCode, errorCode).Set(record => record.ProcessingErrorMessage, errorMessage).Set(record => record.ProcessedAtUtc, now), cancellationToken);

    public async Task<long> CountByUserIdAndStatusAsync(string userId, OfflineIngestionProcessingStatus status, CancellationToken cancellationToken) =>
        await _records.CountDocumentsAsync(record => record.UserId == userId && record.ProcessingStatus == status, cancellationToken: cancellationToken);

    private async Task UpdateProcessingStatusAsync(string id, string userId, OfflineIngestionProcessingStatus status, DateTimeOffset now, UpdateDefinition<OfflineIngestionRecord> extraUpdate, CancellationToken cancellationToken)
    {
        UpdateDefinition<OfflineIngestionRecord> update = Builders<OfflineIngestionRecord>.Update.Combine(
            Builders<OfflineIngestionRecord>.Update.Set(record => record.ProcessingStatus, status),
            Builders<OfflineIngestionRecord>.Update.Set(record => record.UpdatedAtUtc, now),
            extraUpdate);
        await _records.UpdateOneAsync(record => record.Id == id && record.UserId == userId, update, cancellationToken: cancellationToken);
    }
}
