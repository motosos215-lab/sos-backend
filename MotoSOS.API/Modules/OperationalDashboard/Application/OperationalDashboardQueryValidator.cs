using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.Incidents.Domain;

namespace MotoSOS.API.Modules.OperationalDashboard.Application;

public sealed class OperationalDashboardQueryValidator
{
    private const int MaxPageSize = 100;

    public OperationalDashboardQuery ValidateDateRange(DateTimeOffset? dateFrom, DateTimeOffset? dateTo)
    {
        if (dateFrom.HasValue && dateTo.HasValue && dateFrom.Value > dateTo.Value) throw new ValidationAppException("dateFrom cannot be greater than dateTo.");
        return new OperationalDashboardQuery(NormalizeUtc(dateFrom), NormalizeUtc(dateTo), null, null, null);
    }

    public OperationalDashboardQuery ValidateIncidentQuery(DateTimeOffset? dateFrom, DateTimeOffset? dateTo, string? status, int? pageNumber, int? pageSize)
    {
        OperationalDashboardQuery range = ValidateDateRange(dateFrom, dateTo);
        if (pageNumber.HasValue && pageNumber.Value < 1) throw new ValidationAppException("pageNumber must be greater than or equal to 1.");
        if (pageSize.HasValue && pageSize.Value < 1) throw new ValidationAppException("pageSize must be greater than or equal to 1.");
        if (pageSize.HasValue && pageSize.Value > MaxPageSize) throw new ValidationAppException("pageSize cannot be greater than 100.");
        IncidentStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse(status, ignoreCase: false, out IncidentStatus parsed)) throw new ValidationAppException("status is invalid.");
            parsedStatus = parsed;
        }
        return range with { Status = status, PageNumber = pageNumber ?? 1, PageSize = pageSize ?? 20, ParsedStatus = parsedStatus };
    }

    private static DateTimeOffset? NormalizeUtc(DateTimeOffset? value) => value?.ToUniversalTime();
}
