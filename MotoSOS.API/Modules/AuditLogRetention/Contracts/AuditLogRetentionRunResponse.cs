namespace MotoSOS.API.Modules.AuditLogRetention.Contracts;

public sealed record AuditLogRetentionRunResponse(
    string Id,
    string RequestedByUserId,
    string RequestedByRole,
    string Mode,
    string Status,
    int RetentionDays,
    DateTimeOffset CutoffUtc,
    long CandidateCount,
    long DeletedCount,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string? ErrorCode,
    string? ErrorMessage);
