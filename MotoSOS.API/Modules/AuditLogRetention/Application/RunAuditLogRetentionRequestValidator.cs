using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AuditLogRetention.Contracts;

namespace MotoSOS.API.Modules.AuditLogRetention.Application;

public sealed class RunAuditLogRetentionRequestValidator
{
    public ValidatedAuditLogRetentionRunRequest Validate(RunAuditLogRetentionRequest? request)
    {
        int retentionDays = request?.RetentionDays ?? AuditLogRetentionPolicy.RetentionDaysDefault;
        bool dryRun = request?.DryRun ?? AuditLogRetentionPolicy.DryRunDefault;
        bool confirmPermanentDelete = request?.ConfirmPermanentDelete == true;

        if (retentionDays < AuditLogRetentionPolicy.MinimumRetentionDays || retentionDays > AuditLogRetentionPolicy.MaximumRetentionDays)
        {
            throw new ValidationAppException($"retentionDays must be between {AuditLogRetentionPolicy.MinimumRetentionDays} and {AuditLogRetentionPolicy.MaximumRetentionDays}.");
        }

        if (!dryRun && !confirmPermanentDelete)
        {
            throw new ValidationAppException("confirmPermanentDelete must be true when dryRun is false.");
        }

        return new ValidatedAuditLogRetentionRunRequest(retentionDays, dryRun, confirmPermanentDelete);
    }
}

public sealed record ValidatedAuditLogRetentionRunRequest(int RetentionDays, bool DryRun, bool ConfirmPermanentDelete);
