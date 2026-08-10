using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.TelemetrySummary.Domain;
using MotoSOS.API.Modules.Trips.Domain;

namespace MotoSOS.API.Modules.TelemetrySummary.Application;

public sealed class TelemetrySummaryQueryValidator
{
    private const int MaxPageSize = 100;

    public TelemetrySummaryQuery Validate(string? userId, string? tripId, string? tripStatus, string? summaryStatus, DateTimeOffset? dateFrom, DateTimeOffset? dateTo, int? pageNumber, int? pageSize)
    {
        if (dateFrom.HasValue && dateTo.HasValue && dateFrom.Value > dateTo.Value) throw new ValidationAppException("dateFrom cannot be greater than dateTo.");
        if (pageNumber.HasValue && pageNumber.Value < 1) throw new ValidationAppException("pageNumber must be greater than or equal to 1.");
        if (pageSize.HasValue && pageSize.Value < 1) throw new ValidationAppException("pageSize must be greater than or equal to 1.");
        if (pageSize.HasValue && pageSize.Value > MaxPageSize) throw new ValidationAppException("pageSize cannot be greater than 100.");
        return new TelemetrySummaryQuery(Normalize(userId), Normalize(tripId), Parse<TripStatus>(tripStatus, nameof(tripStatus)), Parse<TelemetrySummaryStatus>(summaryStatus, nameof(summaryStatus)), dateFrom?.ToUniversalTime(), dateTo?.ToUniversalTime(), pageNumber ?? 1, pageSize ?? 20);
    }

    private static TEnum? Parse<TEnum>(string? value, string name) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!Enum.TryParse(value.Trim(), false, out TEnum parsed)) throw new ValidationAppException($"{name} is invalid.");
        return parsed;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
