using MotoSOS.API.Modules.AuditLogs.Contracts;
using MotoSOS.API.Modules.AuditLogs.Domain;

namespace MotoSOS.API.Modules.AuditLogs.Application;

public interface IAuditLogService
{
    Task RecordAsync(
        string actorUserId,
        string actorRole,
        AuditAction action,
        AuditModule module,
        string entityType,
        string? entityId,
        AuditOutcome outcome,
        string? reason,
        string? requestPath,
        string? httpMethod,
        IReadOnlyDictionary<string, string>? metadata,
        CancellationToken cancellationToken);

    Task<GetAuditLogsResponse> ListAsync(string adminUserId, AuditLogQuery query, CancellationToken cancellationToken);
    Task<AuditLogResponse> GetAsync(string adminUserId, string id, CancellationToken cancellationToken);
}
