namespace MotoSOS.API.Modules.AuditLogRetention.Contracts;

public sealed record AuditLogRetentionPolicyResponse(
    int RetentionDaysDefault,
    int MinimumRetentionDays,
    int MaximumRetentionDays,
    bool DryRunDefault,
    bool DeleteRequiresConfirmation,
    bool AutomaticWorkerEnabled);
