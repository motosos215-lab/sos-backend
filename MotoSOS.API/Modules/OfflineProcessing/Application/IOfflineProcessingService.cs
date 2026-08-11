using MotoSOS.API.Modules.OfflineProcessing.Contracts;

namespace MotoSOS.API.Modules.OfflineProcessing.Application;

public interface IOfflineProcessingService
{
    Task<RunOfflineProcessingResponse> RunAsync(string userId, RunOfflineProcessingRequest request, CancellationToken cancellationToken);
    Task<GetOfflineProcessingStatusResponse> GetStatusAsync(string userId, CancellationToken cancellationToken);
    Task<RunOfflineProcessingResponse> RunWorkerAsync(int maxItems, int recoveryMinutes, CancellationToken cancellationToken);
    Task<OfflineProcessingWorkerStatusResponse> GetWorkerStatusAsync(string adminUserId, CancellationToken cancellationToken);
}
