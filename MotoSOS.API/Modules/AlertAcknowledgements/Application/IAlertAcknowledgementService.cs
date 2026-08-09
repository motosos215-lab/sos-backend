using MotoSOS.API.Modules.AlertAcknowledgements.Contracts;

namespace MotoSOS.API.Modules.AlertAcknowledgements.Application;

public interface IAlertAcknowledgementService
{
    Task<GetMonitorAlertsResponse> ListMonitorAlertsAsync(string monitorUserId, string? status, int? pageNumber, int? pageSize, CancellationToken cancellationToken);
    Task<ViewAlertResponse> GetMonitorAlertAsync(string monitorUserId, string notificationDeliveryAttemptId, CancellationToken cancellationToken);
    Task<ViewAlertResponse> ViewAsync(string monitorUserId, string notificationDeliveryAttemptId, CancellationToken cancellationToken);
    Task<AcknowledgeAlertResponse> AcknowledgeAsync(string monitorUserId, string notificationDeliveryAttemptId, AcknowledgeAlertRequest request, CancellationToken cancellationToken);
    Task<DeclineAlertResponse> DeclineAsync(string monitorUserId, string notificationDeliveryAttemptId, DeclineAlertRequest request, CancellationToken cancellationToken);
    Task<GetAlertAcknowledgementsResponse> ListRiderAcknowledgementsAsync(string riderUserId, string? alertDispatchId, string? incidentId, string? status, int? pageNumber, int? pageSize, CancellationToken cancellationToken);
}
