using MotoSOS.API.Modules.OfflineIngestion.Domain;

namespace MotoSOS.API.Modules.OfflineIngestion.Application;

public interface IOfflineIngestionRepository
{
    Task<OfflineIngestionRecord?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);
    Task<(OfflineIngestionRecord Record, bool IsDuplicate)> AddOrGetDuplicateAsync(OfflineIngestionRecord record, CancellationToken cancellationToken);
}
