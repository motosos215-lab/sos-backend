using MotoSOS.API.Modules.Incidents.Application;
using MotoSOS.API.Modules.Incidents.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class UnconfiguredIncidentRepository : IIncidentRepository
{
    public Task<Incident?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult<Incident?>(null);
    public Task<Incident?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) => Task.FromResult<Incident?>(null);
    public Task<(Incident Incident, bool IsDuplicate)> AddOrGetDuplicateAsync(Incident incident, CancellationToken cancellationToken) => Task.FromResult((incident, false));
    public Task<IReadOnlyList<Incident>> ListByUserIdAsync(string userId, IncidentStatus? status, string? tripId, int pageNumber, int pageSize, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Incident>>([]);
    public Task<long> CountByUserIdAsync(string userId, IncidentStatus? status, string? tripId, CancellationToken cancellationToken) => Task.FromResult(0L);
    public Task UpdateAsync(Incident incident, CancellationToken cancellationToken) => Task.CompletedTask;
}
