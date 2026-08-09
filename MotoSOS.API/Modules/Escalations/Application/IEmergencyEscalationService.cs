using MotoSOS.API.Modules.Escalations.Contracts;

namespace MotoSOS.API.Modules.Escalations.Application;

public interface IEmergencyEscalationService
{
    Task<CreateEmergencyEscalationResponse> EscalateAsync(string riderUserId, string alertDispatchId, CreateEmergencyEscalationRequest request, CancellationToken cancellationToken);
    Task<GetEmergencyEscalationResponse> GetForRiderAsync(string riderUserId, string alertDispatchId, CancellationToken cancellationToken);
    Task<GetEmergencyEscalationResponse> GetForMonitorAsync(string monitorUserId, string notificationDeliveryAttemptId, CancellationToken cancellationToken);
    Task<GetEmergencyEscalationResponse> MarkUnresolvedAsync(string riderUserId, string alertDispatchId, CancellationToken cancellationToken);
    Task<GetEmergencyEscalationResponse> CancelAsync(string riderUserId, string alertDispatchId, CancellationToken cancellationToken);
    Task<GetEmergencyEscalationsResponse> ListForAdminAsync(string adminUserId, EmergencyEscalationQuery query, CancellationToken cancellationToken);
}
