using MotoSOS.API.Modules.MinorEvents.Domain;

namespace MotoSOS.API.Modules.MinorEvents.Application;

public interface IMinorEventRepository
{
    Task<MinorEvent?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task<MinorEvent?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);
    Task<(MinorEvent MinorEvent, bool IsDuplicate)> AddOrGetDuplicateAsync(MinorEvent minorEvent, CancellationToken cancellationToken);
    Task UpdateAsync(MinorEvent minorEvent, CancellationToken cancellationToken);
    Task<IReadOnlyList<MinorEvent>> ListByUserIdAsync(string userId, MinorEventQuery query, CancellationToken cancellationToken);
    Task<long> CountByUserIdAsync(string userId, MinorEventQuery query, CancellationToken cancellationToken);
    Task<IReadOnlyList<MinorEvent>> ListAsync(MinorEventQuery query, CancellationToken cancellationToken);
    Task<long> CountAsync(MinorEventQuery query, CancellationToken cancellationToken);
}
