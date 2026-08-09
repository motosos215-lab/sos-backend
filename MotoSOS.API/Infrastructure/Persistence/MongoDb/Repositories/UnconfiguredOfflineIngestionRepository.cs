using MotoSOS.API.Modules.OfflineIngestion.Application;
using MotoSOS.API.Modules.OfflineIngestion.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class UnconfiguredOfflineIngestionRepository : IOfflineIngestionRepository
{
    public Task<OfflineIngestionRecord?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) => Task.FromResult<OfflineIngestionRecord?>(null);
    public Task<(OfflineIngestionRecord Record, bool IsDuplicate)> AddOrGetDuplicateAsync(OfflineIngestionRecord record, CancellationToken cancellationToken) => Task.FromResult((record, false));
    public Task<IReadOnlyList<OfflineIngestionRecord>> ListPendingByUserIdAsync(string userId, int maxItems, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OfflineIngestionRecord>>([]);
    public Task<OfflineIngestionRecord?> TryMarkProcessingAsync(string id, string userId, DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult<OfflineIngestionRecord?>(null);
    public Task MarkProcessedAsync(string id, string userId, string remoteRecordId, DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task MarkIgnoredAsync(string id, string userId, string reason, DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task MarkFailedPermanentAsync(string id, string userId, string errorCode, string errorMessage, DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task<long> CountByUserIdAndStatusAsync(string userId, OfflineIngestionProcessingStatus status, CancellationToken cancellationToken) => Task.FromResult(0L);
}
