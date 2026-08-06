using MotoSOS.API.Modules.OfflineIngestion.Application;
using MotoSOS.API.Modules.OfflineIngestion.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class UnconfiguredOfflineIngestionRepository : IOfflineIngestionRepository
{
    public Task<OfflineIngestionRecord?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) => Task.FromResult<OfflineIngestionRecord?>(null);
    public Task<(OfflineIngestionRecord Record, bool IsDuplicate)> AddOrGetDuplicateAsync(OfflineIngestionRecord record, CancellationToken cancellationToken) => Task.FromResult((record, false));
}
