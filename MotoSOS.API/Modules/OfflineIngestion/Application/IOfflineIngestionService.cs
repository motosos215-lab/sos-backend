using MotoSOS.API.Modules.OfflineIngestion.Contracts;

namespace MotoSOS.API.Modules.OfflineIngestion.Application;

public interface IOfflineIngestionService
{
    Task<OfflineIngestionBatchResponse> IngestBatchAsync(string userId, OfflineIngestionBatchRequest request, CancellationToken cancellationToken);
}
