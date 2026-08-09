using MotoSOS.API.Modules.AuditLogs.Domain;

namespace MotoSOS.API.Modules.AuditLogs.Application;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLogEntry entry, CancellationToken cancellationToken);
    Task<AuditLogEntry?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task<IReadOnlyList<AuditLogEntry>> ListAsync(AuditLogQuery query, CancellationToken cancellationToken);
    Task<long> CountAsync(AuditLogQuery query, CancellationToken cancellationToken);
}
