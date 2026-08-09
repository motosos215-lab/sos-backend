namespace MotoSOS.API.Modules.AuditLogs.Contracts;

public sealed record GetAuditLogsResponse(IReadOnlyList<AuditLogResponse> AuditLogs, int PageNumber, int PageSize, long TotalCount);
