using MotoSOS.API.Modules.AuditLogs.Application;
using MotoSOS.API.Modules.AuditLogs.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class UnconfiguredAuditLogRepository : IAuditLogRepository
{
    private static InvalidOperationException CreateException() => new("MongoDB is not configured. Configure MongoDB settings to use Audit Logs API.");
    public Task AddAsync(AuditLogEntry entry, CancellationToken cancellationToken) => throw CreateException();
    public Task<AuditLogEntry?> GetByIdAsync(string id, CancellationToken cancellationToken) => throw CreateException();
    public Task<IReadOnlyList<AuditLogEntry>> ListAsync(AuditLogQuery query, CancellationToken cancellationToken) => throw CreateException();
    public Task<long> CountAsync(AuditLogQuery query, CancellationToken cancellationToken) => throw CreateException();
}
