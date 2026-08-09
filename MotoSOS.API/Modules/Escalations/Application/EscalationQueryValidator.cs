using System.Globalization;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.Escalations.Domain;

namespace MotoSOS.API.Modules.Escalations.Application;

public sealed class EscalationQueryValidator
{
    private const int MaxPageSize = 100;
    public EmergencyEscalationQuery Validate(string? status, string? reason, string? level, DateTimeOffset? dateFrom, DateTimeOffset? dateTo, int? pageNumber, int? pageSize)
    {
        if (dateFrom.HasValue && dateTo.HasValue && dateFrom.Value > dateTo.Value) throw new ValidationAppException("dateFrom cannot be greater than dateTo.");
        if (pageNumber.HasValue && pageNumber.Value < 1) throw new ValidationAppException("pageNumber must be greater than or equal to 1.");
        if (pageSize.HasValue && pageSize.Value < 1) throw new ValidationAppException("pageSize must be greater than or equal to 1.");
        if (pageSize.HasValue && pageSize.Value > MaxPageSize) throw new ValidationAppException("pageSize cannot be greater than 100.");
        return new EmergencyEscalationQuery(Parse<EmergencyEscalationStatus>(status, nameof(status), false), Parse<EmergencyEscalationReason>(reason, nameof(reason), true), Parse<EmergencyEscalationLevel>(level, nameof(level), false), dateFrom?.ToUniversalTime(), dateTo?.ToUniversalTime(), pageNumber ?? 1, pageSize ?? 20);
    }

    private static TEnum? Parse<TEnum>(string? value, string name, bool disallowUnknown) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!Enum.TryParse(value.Trim(), false, out TEnum parsed)) throw new ValidationAppException($"{name} is invalid.");
        if (disallowUnknown && Convert.ToInt32(parsed, CultureInfo.InvariantCulture) == 0) throw new ValidationAppException($"{name} is invalid.");
        return parsed;
    }
}
