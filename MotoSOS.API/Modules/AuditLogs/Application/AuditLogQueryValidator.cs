using System.Globalization;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AuditLogs.Domain;

namespace MotoSOS.API.Modules.AuditLogs.Application;

public sealed class AuditLogQueryValidator
{
    public const int DefaultPageNumber = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public AuditLogQuery Validate(
        string? actorUserId,
        string? action,
        string? module,
        string? outcome,
        string? entityType,
        string? entityId,
        DateTimeOffset? dateFrom,
        DateTimeOffset? dateTo,
        int? pageNumber,
        int? pageSize)
    {
        if (dateFrom.HasValue && dateTo.HasValue && dateFrom.Value > dateTo.Value)
        {
            throw new ValidationAppException("dateFrom cannot be greater than dateTo.");
        }

        if (pageNumber.HasValue && pageNumber.Value < 1) throw new ValidationAppException("pageNumber must be greater than or equal to 1.");
        if (pageSize.HasValue && pageSize.Value < 1) throw new ValidationAppException("pageSize must be greater than or equal to 1.");
        if (pageSize.HasValue && pageSize.Value > MaxPageSize) throw new ValidationAppException("pageSize cannot be greater than 100.");

        return new AuditLogQuery(
            Normalize(actorUserId),
            ParseOptional<AuditAction>(action, nameof(action), disallowUnknown: true),
            ParseOptional<AuditModule>(module, nameof(module), disallowUnknown: true),
            ParseOptional<AuditOutcome>(outcome, nameof(outcome), disallowUnknown: false),
            Normalize(entityType),
            Normalize(entityId),
            dateFrom?.ToUniversalTime(),
            dateTo?.ToUniversalTime(),
            pageNumber ?? DefaultPageNumber,
            pageSize ?? DefaultPageSize);
    }

    private static TEnum? ParseOptional<TEnum>(string? value, string name, bool disallowUnknown) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!Enum.TryParse(value.Trim(), ignoreCase: false, out TEnum parsed)) throw new ValidationAppException($"{name} is invalid.");
        if (disallowUnknown && Convert.ToInt32(parsed, CultureInfo.InvariantCulture) == 0) throw new ValidationAppException($"{name} is invalid.");
        return parsed;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
