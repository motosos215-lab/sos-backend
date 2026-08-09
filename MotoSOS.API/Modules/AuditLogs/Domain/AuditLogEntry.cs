namespace MotoSOS.API.Modules.AuditLogs.Domain;

public sealed class AuditLogEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string ActorUserId { get; set; } = string.Empty;

    public string ActorRole { get; set; } = string.Empty;

    public AuditAction Action { get; set; } = AuditAction.Unknown;

    public AuditModule Module { get; set; } = AuditModule.Unknown;

    public string EntityType { get; set; } = string.Empty;

    public string? EntityId { get; set; }

    public AuditOutcome Outcome { get; set; } = AuditOutcome.Success;

    public string? Reason { get; set; }

    public string? CorrelationId { get; set; }

    public string? RequestPath { get; set; }

    public string? HttpMethod { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Dictionary<string, string> Metadata { get; set; } = [];
}
