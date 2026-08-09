using MotoSOS.API.Modules.AuditLogRetention.Application;
using MotoSOS.API.Modules.AuditLogRetention.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class UnconfiguredAuditLogRetentionRunRepository : IAuditLogRetentionRunRepository
{
    private static InvalidOperationException CreateException() => new("MongoDB is not configured. Configure MongoDB settings to use Audit Log Retention API.");
    public Task AddAsync(AuditLogRetentionRun run, CancellationToken cancellationToken) => throw CreateException();
    public Task<AuditLogRetentionRun?> GetByIdAsync(string id, CancellationToken cancellationToken) => throw CreateException();
    public Task<IReadOnlyList<AuditLogRetentionRun>> ListAsync(AuditLogRetentionRunQuery query, CancellationToken cancellationToken) => throw CreateException();
    public Task<long> CountAsync(CancellationToken cancellationToken) => throw CreateException();
}
