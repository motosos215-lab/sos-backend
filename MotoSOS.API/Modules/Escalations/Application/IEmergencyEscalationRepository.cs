using MotoSOS.API.Modules.Escalations.Domain;

namespace MotoSOS.API.Modules.Escalations.Application;

public interface IEmergencyEscalationRepository
{
    Task<EmergencyEscalation?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task<EmergencyEscalation?> GetByAlertDispatchIdAsync(string alertDispatchId, CancellationToken cancellationToken);
    Task<EmergencyEscalation?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);
    Task<(EmergencyEscalation Escalation, bool IsDuplicate)> AddOrGetDuplicateAsync(EmergencyEscalation escalation, CancellationToken cancellationToken);
    Task<IReadOnlyList<EmergencyEscalation>> ListAsync(EmergencyEscalationQuery query, CancellationToken cancellationToken);
    Task<long> CountAsync(EmergencyEscalationQuery query, CancellationToken cancellationToken);
    Task UpdateAsync(EmergencyEscalation escalation, CancellationToken cancellationToken);
}
