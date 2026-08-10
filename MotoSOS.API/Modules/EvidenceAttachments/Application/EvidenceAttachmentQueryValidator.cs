using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.EvidenceAttachments.Domain;

namespace MotoSOS.API.Modules.EvidenceAttachments.Application;

public sealed class EvidenceAttachmentQueryValidator
{
    private const int MaxPageSize = 100;

    public EvidenceAttachmentQuery Validate(string? userId, string? incidentId, string? alertDispatchId, string? emergencyResolutionReportId, string? evidenceType, string? source, string? status, DateTimeOffset? dateFrom, DateTimeOffset? dateTo, int? pageNumber, int? pageSize)
    {
        if (dateFrom.HasValue && dateTo.HasValue && dateFrom.Value > dateTo.Value) throw new ValidationAppException("dateFrom cannot be greater than dateTo.");
        if (pageNumber.HasValue && pageNumber.Value < 1) throw new ValidationAppException("pageNumber must be greater than or equal to 1.");
        if (pageSize.HasValue && pageSize.Value < 1) throw new ValidationAppException("pageSize must be greater than or equal to 1.");
        if (pageSize.HasValue && pageSize.Value > MaxPageSize) throw new ValidationAppException("pageSize cannot be greater than 100.");
        return new EvidenceAttachmentQuery(Normalize(userId), Normalize(incidentId), Normalize(alertDispatchId), Normalize(emergencyResolutionReportId), Parse<EvidenceType>(evidenceType, nameof(evidenceType), true), Parse<EvidenceSource>(source, nameof(source), true), Parse<EvidenceAttachmentStatus>(status, nameof(status), false), dateFrom?.ToUniversalTime(), dateTo?.ToUniversalTime(), pageNumber ?? 1, pageSize ?? 20);
    }

    private static TEnum? Parse<TEnum>(string? value, string name, bool disallowUnknown) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!Enum.TryParse(value.Trim(), false, out TEnum parsed)) throw new ValidationAppException($"{name} is invalid.");
        if (disallowUnknown && Convert.ToInt32(parsed, System.Globalization.CultureInfo.InvariantCulture) == 0) throw new ValidationAppException($"{name} is invalid.");
        return parsed;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
