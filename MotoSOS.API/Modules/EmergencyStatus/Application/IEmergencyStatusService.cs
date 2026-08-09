using MotoSOS.API.Modules.EmergencyStatus.Contracts;

namespace MotoSOS.API.Modules.EmergencyStatus.Application;

public interface IEmergencyStatusService
{
    Task<EmergencyStatusResponse> GetForRiderAsync(string riderUserId, string incidentId, CancellationToken cancellationToken);
    Task<EmergencyStatusResponse> GetForMonitorAsync(string monitorUserId, string notificationDeliveryAttemptId, CancellationToken cancellationToken);
    Task<GetActiveEmergenciesResponse> ListActiveForRiderAsync(string riderUserId, int? pageNumber, int? pageSize, CancellationToken cancellationToken);
}
