namespace MotoSOS.API.Modules.AuditLogs.Contracts;

public sealed record AuditLogResponse(
    string Id,
    string ActorUserId,
    string ActorRole,
    string Action,
    string Module,
    string EntityType,
    string? EntityId,
    string Outcome,
    string? Reason,
    string? CorrelationId,
    string? RequestPath,
    string? HttpMethod,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<AuditLogMetadataItemResponse> Metadata);
