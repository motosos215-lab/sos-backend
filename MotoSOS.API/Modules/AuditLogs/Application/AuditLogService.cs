using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AuditLogs.Contracts;
using MotoSOS.API.Modules.AuditLogs.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Modules.AuditLogs.Application;

public sealed class AuditLogService : IAuditLogService
{
    public const int MetadataValueMaxLength = 200;

    private static readonly string[] SensitiveKeyFragments =
    [
        "password",
        "passwordhash",
        "accesstoken",
        "refreshtoken",
        "token",
        "authorization",
        "bearer",
        "deviceidentifier",
        "deviceidentifierhash",
        "providertoken",
        "payload",
        "email",
        "phone",
        "payment",
        "card",
        "connectionstring",
        "secret",
        "stacktrace",
        "exception",
        "mongo",
        "mongodb"
    ];

    private readonly IUserRepository _users;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IClock _clock;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(IUserRepository users, IAuditLogRepository auditLogs, IClock clock, ILogger<AuditLogService> logger, IHttpContextAccessor? httpContextAccessor = null)
    {
        _users = users;
        _auditLogs = auditLogs;
        _clock = clock;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task RecordAsync(
        string actorUserId,
        string actorRole,
        AuditAction action,
        AuditModule module,
        string entityType,
        string? entityId,
        AuditOutcome outcome,
        string? reason,
        string? requestPath,
        string? httpMethod,
        IReadOnlyDictionary<string, string>? metadata,
        CancellationToken cancellationToken)
    {
        try
        {
            HttpContext? context = _httpContextAccessor?.HttpContext;
            var entry = new AuditLogEntry
            {
                ActorUserId = Normalize(actorUserId) ?? string.Empty,
                ActorRole = Normalize(actorRole) ?? string.Empty,
                Action = action,
                Module = module,
                EntityType = Normalize(entityType) ?? string.Empty,
                EntityId = Normalize(entityId),
                Outcome = outcome,
                Reason = Truncate(Normalize(reason)),
                CorrelationId = Normalize(context?.TraceIdentifier),
                RequestPath = Normalize(requestPath) ?? Normalize(context?.Request.Path.Value),
                HttpMethod = Normalize(httpMethod) ?? Normalize(context?.Request.Method),
                CreatedAtUtc = _clock.UtcNow,
                Metadata = SanitizeMetadata(metadata)
            };

            await _auditLogs.AddAsync(entry, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Audit log write failed. Action: {Action}. Module: {Module}. ExceptionType: {ExceptionType}.", action, module, exception.GetType().Name);
        }
    }

    public async Task<GetAuditLogsResponse> ListAsync(string adminUserId, AuditLogQuery query, CancellationToken cancellationToken)
    {
        await EnsureAdminAsync(adminUserId, cancellationToken);
        IReadOnlyList<AuditLogEntry> logs = await _auditLogs.ListAsync(query, cancellationToken);
        long total = await _auditLogs.CountAsync(query, cancellationToken);
        return new GetAuditLogsResponse(logs.Select(ToResponse).ToArray(), query.PageNumber, query.PageSize, total);
    }

    public async Task<AuditLogResponse> GetAsync(string adminUserId, string id, CancellationToken cancellationToken)
    {
        await EnsureAdminAsync(adminUserId, cancellationToken);
        AuditLogEntry entry = await _auditLogs.GetByIdAsync(id.Trim(), cancellationToken) ?? throw new NotFoundAppException("Audit log was not found.");
        return ToResponse(entry);
    }

    public static Dictionary<string, string> SanitizeMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0) return [];

        var sanitized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach ((string key, string value) in metadata)
        {
            string? normalizedKey = Normalize(key);
            if (normalizedKey is null || IsSensitiveKey(normalizedKey)) continue;
            sanitized[normalizedKey] = Truncate(Normalize(value)) ?? string.Empty;
        }

        return sanitized;
    }

    private async Task<User> EnsureAdminAsync(string userId, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive) throw new UnauthorizedAppException("Invalid authentication credentials.");
        if (user.Role != UserRole.Admin) throw new ForbiddenAppException("Audit Logs API is available only for admins.");
        return user;
    }

    private static AuditLogResponse ToResponse(AuditLogEntry entry) => new(
        entry.Id,
        entry.ActorUserId,
        entry.ActorRole,
        entry.Action.ToString(),
        entry.Module.ToString(),
        entry.EntityType,
        entry.EntityId,
        entry.Outcome.ToString(),
        entry.Reason,
        entry.CorrelationId,
        entry.RequestPath,
        entry.HttpMethod,
        entry.CreatedAtUtc,
        SanitizeMetadata(entry.Metadata).OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => new AuditLogMetadataItemResponse(item.Key, item.Value)).ToArray());

    private static bool IsSensitiveKey(string key)
    {
        string normalized = key.Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal).Trim();
        return SensitiveKeyFragments.Any(fragment => normalized.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Truncate(string? value) => value is null || value.Length <= MetadataValueMaxLength ? value : value[..MetadataValueMaxLength];
}
