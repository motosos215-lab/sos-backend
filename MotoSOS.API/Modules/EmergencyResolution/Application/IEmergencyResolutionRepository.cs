using MotoSOS.API.Modules.EmergencyResolution.Domain;

namespace MotoSOS.API.Modules.EmergencyResolution.Application;

public interface IEmergencyResolutionRepository
{
    Task<EmergencyResolutionReport?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task<EmergencyResolutionReport?> GetByIncidentIdAsync(string incidentId, CancellationToken cancellationToken);
    Task<EmergencyResolutionReport?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);
    Task<(EmergencyResolutionReport Report, bool IsDuplicate)> AddOrGetDuplicateAsync(EmergencyResolutionReport report, CancellationToken cancellationToken);
    Task<IReadOnlyList<EmergencyResolutionReport>> ListByUserIdAsync(string userId, EmergencyResolutionOutcome? outcome, int pageNumber, int pageSize, CancellationToken cancellationToken);
    Task<long> CountByUserIdAsync(string userId, EmergencyResolutionOutcome? outcome, CancellationToken cancellationToken);
}
