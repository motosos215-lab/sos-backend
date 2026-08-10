namespace MotoSOS.API.Modules.AuditLogRetention.Contracts;

public sealed record RunAuditLogRetentionRequest(int? RetentionDays, bool? DryRun, bool? ConfirmPermanentDelete);
