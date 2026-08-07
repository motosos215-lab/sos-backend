using MotoSOS.API.Modules.AlertAcknowledgements.Application;
using MotoSOS.API.Modules.AlertAcknowledgements.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class UnconfiguredAlertAcknowledgementRepository : IAlertAcknowledgementRepository
{
    private static InvalidOperationException CreateException() => new("MongoDB is not configured. Configure MongoDb:ConnectionString and MongoDb:DatabaseName to use Alert Acknowledgements API.");
    public Task<AlertAcknowledgement?> GetByIdAsync(string id, CancellationToken cancellationToken) => throw CreateException();
    public Task<AlertAcknowledgement?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) => throw CreateException();
    public Task<(AlertAcknowledgement Acknowledgement, bool IsDuplicate)> AddOrGetDuplicateAsync(AlertAcknowledgement acknowledgement, CancellationToken cancellationToken) => throw CreateException();
    public Task<IReadOnlyList<AlertAcknowledgement>> ListByIncidentIdAsync(string userId, string incidentId, CancellationToken cancellationToken) => throw CreateException();
    public Task<IReadOnlyList<AlertAcknowledgement>> ListByAlertDispatchIdAsync(string userId, string alertDispatchId, CancellationToken cancellationToken) => throw CreateException();
    public Task<IReadOnlyList<AlertAcknowledgement>> ListByMonitorUserIdAsync(string monitorUserId, AlertAcknowledgementStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken) => throw CreateException();
    public Task<long> CountByMonitorUserIdAsync(string monitorUserId, AlertAcknowledgementStatus? status, CancellationToken cancellationToken) => throw CreateException();
    public Task<IReadOnlyList<AlertAcknowledgement>> ListByUserIdAsync(string userId, string? alertDispatchId, string? incidentId, AlertAcknowledgementStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken) => throw CreateException();
    public Task<long> CountByUserIdAsync(string userId, string? alertDispatchId, string? incidentId, AlertAcknowledgementStatus? status, CancellationToken cancellationToken) => throw CreateException();
    public Task UpdateAsync(AlertAcknowledgement acknowledgement, CancellationToken cancellationToken) => throw CreateException();
}
