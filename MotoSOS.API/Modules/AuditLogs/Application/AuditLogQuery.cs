using MotoSOS.API.Modules.AuditLogs.Domain;

namespace MotoSOS.API.Modules.AuditLogs.Application;

public sealed record AuditLogQuery(
    string? ActorUserId,
    AuditAction? Action,
    AuditModule? Module,
    AuditOutcome? Outcome,
    string? EntityType,
    string? EntityId,
    DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo,
    int PageNumber,
    int PageSize);
