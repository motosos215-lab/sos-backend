using MotoSOS.API.Modules.MinorEvents.Application;
using MotoSOS.API.Modules.MinorEvents.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class UnconfiguredMinorEventRepository : IMinorEventRepository
{
    private static InvalidOperationException Unconfigured() => new("MongoDB is not configured.");
    public Task<MinorEvent?> GetByIdAsync(string id, CancellationToken cancellationToken) => throw Unconfigured();
    public Task<MinorEvent?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) => throw Unconfigured();
    public Task<(MinorEvent MinorEvent, bool IsDuplicate)> AddOrGetDuplicateAsync(MinorEvent minorEvent, CancellationToken cancellationToken) => throw Unconfigured();
    public Task UpdateAsync(MinorEvent minorEvent, CancellationToken cancellationToken) => throw Unconfigured();
    public Task<IReadOnlyList<MinorEvent>> ListByUserIdAsync(string userId, MinorEventQuery query, CancellationToken cancellationToken) => throw Unconfigured();
    public Task<long> CountByUserIdAsync(string userId, MinorEventQuery query, CancellationToken cancellationToken) => throw Unconfigured();
    public Task<IReadOnlyList<MinorEvent>> ListAsync(MinorEventQuery query, CancellationToken cancellationToken) => throw Unconfigured();
    public Task<long> CountAsync(MinorEventQuery query, CancellationToken cancellationToken) => throw Unconfigured();
}
