using MotoSOS.API.Modules.AlertDispatch.Contracts;

namespace MotoSOS.API.Modules.AlertDispatch.Application;

public interface IAlertDispatchService
{
    Task<CreateAlertDispatchResponse> CreateAsync(string userId, CreateAlertDispatchRequest request, CancellationToken cancellationToken);
    Task<GetAlertDispatchesResponse> ListAsync(string userId, string? status, string? incidentId, int? pageNumber, int? pageSize, CancellationToken cancellationToken);
    Task<GetAlertDispatchResponse> GetAsync(string userId, string id, CancellationToken cancellationToken);
    Task<CancelAlertDispatchResponse> CancelAsync(string userId, string id, CancelAlertDispatchRequest request, CancellationToken cancellationToken);
}
