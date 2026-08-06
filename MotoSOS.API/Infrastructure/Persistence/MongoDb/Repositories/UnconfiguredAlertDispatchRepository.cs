using MotoSOS.API.Modules.AlertDispatch.Application;
using MotoSOS.API.Modules.AlertDispatch.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class UnconfiguredAlertDispatchRepository : IAlertDispatchRepository
{
    private static InvalidOperationException CreateException() => new("MongoDB is not configured. Configure MongoDb:ConnectionString and MongoDb:DatabaseName to use Alert Dispatch API.");
    public Task<AlertDispatchRequest?> GetByIdAsync(string id, CancellationToken cancellationToken) => throw CreateException();
    public Task<AlertDispatchRequest?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) => throw CreateException();
    public Task<(AlertDispatchRequest AlertDispatch, bool IsDuplicate)> AddOrGetDuplicateAsync(AlertDispatchRequest alertDispatch, CancellationToken cancellationToken) => throw CreateException();
    public Task<IReadOnlyList<AlertDispatchRequest>> ListByUserIdAsync(string userId, AlertDispatchStatus? status, string? incidentId, int pageNumber, int pageSize, CancellationToken cancellationToken) => throw CreateException();
    public Task<long> CountByUserIdAsync(string userId, AlertDispatchStatus? status, string? incidentId, CancellationToken cancellationToken) => throw CreateException();
    public Task UpdateAsync(AlertDispatchRequest alertDispatch, CancellationToken cancellationToken) => throw CreateException();
}
