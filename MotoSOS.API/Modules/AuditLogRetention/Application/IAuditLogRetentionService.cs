using MotoSOS.API.Modules.AuditLogRetention.Contracts;

namespace MotoSOS.API.Modules.AuditLogRetention.Application;

public interface IAuditLogRetentionService
{
    Task<AuditLogRetentionPolicyResponse> GetPolicyAsync(string adminUserId, CancellationToken cancellationToken);
    Task<AuditLogRetentionRunResponse> RunAsync(string adminUserId, ValidatedAuditLogRetentionRunRequest request, CancellationToken cancellationToken);
    Task<GetAuditLogRetentionRunsResponse> ListRunsAsync(string adminUserId, AuditLogRetentionRunQuery query, CancellationToken cancellationToken);
    Task<AuditLogRetentionRunResponse> GetRunAsync(string adminUserId, string id, CancellationToken cancellationToken);
}
