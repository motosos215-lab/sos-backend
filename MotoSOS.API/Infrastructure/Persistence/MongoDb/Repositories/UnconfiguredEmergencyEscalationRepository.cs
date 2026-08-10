using MotoSOS.API.Modules.Escalations.Application;
using MotoSOS.API.Modules.Escalations.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class UnconfiguredEmergencyEscalationRepository : IEmergencyEscalationRepository
{
    private static InvalidOperationException CreateException() => new("MongoDB is not configured. Configure MongoDB settings to use Emergency Escalation API.");
    public Task<EmergencyEscalation?> GetByIdAsync(string id, CancellationToken cancellationToken) => throw CreateException();
    public Task<EmergencyEscalation?> GetByAlertDispatchIdAsync(string alertDispatchId, CancellationToken cancellationToken) => throw CreateException();
    public Task<EmergencyEscalation?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) => throw CreateException();
    public Task<(EmergencyEscalation Escalation, bool IsDuplicate)> AddOrGetDuplicateAsync(EmergencyEscalation escalation, CancellationToken cancellationToken) => throw CreateException();
    public Task<IReadOnlyList<EmergencyEscalation>> ListAsync(EmergencyEscalationQuery query, CancellationToken cancellationToken) => throw CreateException();
    public Task<long> CountAsync(EmergencyEscalationQuery query, CancellationToken cancellationToken) => throw CreateException();
    public Task UpdateAsync(EmergencyEscalation escalation, CancellationToken cancellationToken) => throw CreateException();
}
