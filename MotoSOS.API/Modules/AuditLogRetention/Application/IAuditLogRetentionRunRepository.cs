using MotoSOS.API.Modules.AuditLogRetention.Domain;

namespace MotoSOS.API.Modules.AuditLogRetention.Application;

public interface IAuditLogRetentionRunRepository
{
    Task AddAsync(AuditLogRetentionRun run, CancellationToken cancellationToken);
    Task<AuditLogRetentionRun?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task<IReadOnlyList<AuditLogRetentionRun>> ListAsync(AuditLogRetentionRunQuery query, CancellationToken cancellationToken);
    Task<long> CountAsync(CancellationToken cancellationToken);
}
