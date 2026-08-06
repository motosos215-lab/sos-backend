using MotoSOS.API.Modules.Incidents.Domain;

namespace MotoSOS.API.Modules.Incidents.Application;

public interface IIncidentRepository
{
    Task<Incident?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task<Incident?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);
    Task<(Incident Incident, bool IsDuplicate)> AddOrGetDuplicateAsync(Incident incident, CancellationToken cancellationToken);
    Task<IReadOnlyList<Incident>> ListByUserIdAsync(string userId, IncidentStatus? status, string? tripId, int pageNumber, int pageSize, CancellationToken cancellationToken);
    Task<long> CountByUserIdAsync(string userId, IncidentStatus? status, string? tripId, CancellationToken cancellationToken);
    Task UpdateAsync(Incident incident, CancellationToken cancellationToken);
}
