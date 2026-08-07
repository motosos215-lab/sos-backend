using MotoSOS.API.Modules.AlertAcknowledgements.Domain;

namespace MotoSOS.API.Modules.AlertAcknowledgements.Application;

public interface IAlertAcknowledgementRepository
{
    Task<AlertAcknowledgement?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task<AlertAcknowledgement?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);
    Task<(AlertAcknowledgement Acknowledgement, bool IsDuplicate)> AddOrGetDuplicateAsync(AlertAcknowledgement acknowledgement, CancellationToken cancellationToken);
    Task<IReadOnlyList<AlertAcknowledgement>> ListByIncidentIdAsync(string userId, string incidentId, CancellationToken cancellationToken) => ListByUserIdAsync(userId, null, incidentId, null, 1, int.MaxValue, cancellationToken);
    Task<IReadOnlyList<AlertAcknowledgement>> ListByAlertDispatchIdAsync(string userId, string alertDispatchId, CancellationToken cancellationToken) => ListByUserIdAsync(userId, alertDispatchId, null, null, 1, int.MaxValue, cancellationToken);
    Task<IReadOnlyList<AlertAcknowledgement>> ListByMonitorUserIdAsync(string monitorUserId, AlertAcknowledgementStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken);
    Task<long> CountByMonitorUserIdAsync(string monitorUserId, AlertAcknowledgementStatus? status, CancellationToken cancellationToken);
    Task<IReadOnlyList<AlertAcknowledgement>> ListByUserIdAsync(string userId, string? alertDispatchId, string? incidentId, AlertAcknowledgementStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken);
    Task<long> CountByUserIdAsync(string userId, string? alertDispatchId, string? incidentId, AlertAcknowledgementStatus? status, CancellationToken cancellationToken);
    Task UpdateAsync(AlertAcknowledgement acknowledgement, CancellationToken cancellationToken);
}
