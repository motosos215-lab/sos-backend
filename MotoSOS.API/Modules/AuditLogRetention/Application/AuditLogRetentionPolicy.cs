using MotoSOS.API.Modules.AuditLogRetention.Contracts;

namespace MotoSOS.API.Modules.AuditLogRetention.Application;

public static class AuditLogRetentionPolicy
{
    public const int RetentionDaysDefault = 180;
    public const int MinimumRetentionDays = 90;
    public const int MaximumRetentionDays = 3650;
    public const bool DryRunDefault = true;
    public const bool DeleteRequiresConfirmation = true;
    public const bool AutomaticWorkerEnabled = false;

    public static AuditLogRetentionPolicyResponse ToResponse() => new(
        RetentionDaysDefault,
        MinimumRetentionDays,
        MaximumRetentionDays,
        DryRunDefault,
        DeleteRequiresConfirmation,
        AutomaticWorkerEnabled);
}
