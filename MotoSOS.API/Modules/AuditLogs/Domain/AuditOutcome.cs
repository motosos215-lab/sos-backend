namespace MotoSOS.API.Modules.AuditLogs.Domain;

public enum AuditOutcome
{
    Success = 1,
    Failed = 2,
    Forbidden = 3,
    ValidationError = 4,
    NotFound = 5,
    Skipped = 6
}
