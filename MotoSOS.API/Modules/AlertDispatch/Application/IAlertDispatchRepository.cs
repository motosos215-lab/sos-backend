using MotoSOS.API.Modules.AlertDispatch.Domain;

namespace MotoSOS.API.Modules.AlertDispatch.Application;

public interface IAlertDispatchRepository
{
    Task<AlertDispatchRequest?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task<AlertDispatchRequest?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);
    Task<(AlertDispatchRequest AlertDispatch, bool IsDuplicate)> AddOrGetDuplicateAsync(AlertDispatchRequest alertDispatch, CancellationToken cancellationToken);
    Task<IReadOnlyList<AlertDispatchRequest>> ListByUserIdAsync(string userId, AlertDispatchStatus? status, string? incidentId, int pageNumber, int pageSize, CancellationToken cancellationToken);
    Task<long> CountByUserIdAsync(string userId, AlertDispatchStatus? status, string? incidentId, CancellationToken cancellationToken);
    Task UpdateAsync(AlertDispatchRequest alertDispatch, CancellationToken cancellationToken);
}
