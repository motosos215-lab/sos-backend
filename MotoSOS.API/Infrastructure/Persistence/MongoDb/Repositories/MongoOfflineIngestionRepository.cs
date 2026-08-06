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
}
