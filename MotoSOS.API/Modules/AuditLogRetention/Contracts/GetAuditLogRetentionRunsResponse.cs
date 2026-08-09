namespace MotoSOS.API.Modules.AuditLogRetention.Contracts;

public sealed record GetAuditLogRetentionRunsResponse(IReadOnlyList<AuditLogRetentionRunResponse> Items, int PageNumber, int PageSize, long TotalCount);
