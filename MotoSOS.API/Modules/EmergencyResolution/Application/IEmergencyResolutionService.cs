using MotoSOS.API.Modules.EmergencyResolution.Contracts;

namespace MotoSOS.API.Modules.EmergencyResolution.Application;

public interface IEmergencyResolutionService
{
    Task<CreateEmergencyResolutionReportResponse> CreateForRiderAsync(string riderUserId, string incidentId, CreateEmergencyResolutionReportRequest request, CancellationToken cancellationToken);
    Task<GetEmergencyResolutionReportResponse> GetForRiderAsync(string riderUserId, string incidentId, CancellationToken cancellationToken);
    Task<GetEmergencyResolutionReportResponse> GetForMonitorAsync(string monitorUserId, string notificationDeliveryAttemptId, CancellationToken cancellationToken);
    Task<GetEmergencyResolutionReportsResponse> ListForRiderAsync(string riderUserId, string? outcome, int? pageNumber, int? pageSize, CancellationToken cancellationToken);
}
