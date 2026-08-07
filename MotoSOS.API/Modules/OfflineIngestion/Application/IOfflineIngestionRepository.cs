using MotoSOS.API.Modules.OfflineIngestion.Domain;

namespace MotoSOS.API.Modules.OfflineIngestion.Application;

public interface IOfflineIngestionRepository
{
    Task<OfflineIngestionRecord?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);
    Task<(OfflineIngestionRecord Record, bool IsDuplicate)> AddOrGetDuplicateAsync(OfflineIngestionRecord record, CancellationToken cancellationToken);
    Task<IReadOnlyList<OfflineIngestionRecord>> ListPendingByUserIdAsync(string userId, int maxItems, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OfflineIngestionRecord>>([]);
    Task<OfflineIngestionRecord?> TryMarkProcessingAsync(string id, string userId, DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult<OfflineIngestionRecord?>(null);
    Task MarkProcessedAsync(string id, string userId, string remoteRecordId, DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
    Task MarkIgnoredAsync(string id, string userId, string reason, DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
    Task MarkFailedPermanentAsync(string id, string userId, string errorCode, string errorMessage, DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
    Task<long> CountByUserIdAndStatusAsync(string userId, OfflineIngestionProcessingStatus status, CancellationToken cancellationToken) => Task.FromResult(0L);
}
