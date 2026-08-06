using MotoSOS.API.Modules.Incidents.Contracts;

namespace MotoSOS.API.Modules.Incidents.Application;

public interface IIncidentService
{
    Task<CreateIncidentResponse> CreateAsync(string userId, CreateIncidentRequest request, CancellationToken cancellationToken);
    Task<GetIncidentsResponse> ListAsync(string userId, string? status, string? tripId, int? pageNumber, int? pageSize, CancellationToken cancellationToken);
    Task<GetIncidentResponse> GetAsync(string userId, string incidentId, CancellationToken cancellationToken);
    Task<CancelFalsePositiveResponse> CancelFalsePositiveAsync(string userId, string incidentId, CancelFalsePositiveRequest request, CancellationToken cancellationToken);
    Task<CloseIncidentResponse> CloseAsync(string userId, string incidentId, CloseIncidentRequest request, CancellationToken cancellationToken);
}
