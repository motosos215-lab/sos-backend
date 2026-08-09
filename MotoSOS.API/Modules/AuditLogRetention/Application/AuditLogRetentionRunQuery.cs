namespace MotoSOS.API.Modules.AuditLogRetention.Application;

public sealed record AuditLogRetentionRunQuery(int PageNumber = 1, int PageSize = 50);
