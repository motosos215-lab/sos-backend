using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.ReportExports.Domain;

namespace MotoSOS.API.Modules.ReportExports.Application;

public sealed class ResolutionReportExportQueryValidator
{
    private const int MaxPageSize = 100;

    public ResolutionReportExportQuery Validate(string? userId, string? incidentId, string? emergencyResolutionReportId, string? exportType, string? status, DateTimeOffset? dateFrom, DateTimeOffset? dateTo, int? pageNumber, int? pageSize)
    {
        if (dateFrom.HasValue && dateTo.HasValue && dateFrom.Value > dateTo.Value) throw new ValidationAppException("dateFrom cannot be greater than dateTo.");
        if (pageNumber.HasValue && pageNumber.Value < 1) throw new ValidationAppException("pageNumber must be greater than or equal to 1.");
        if (pageSize.HasValue && pageSize.Value < 1) throw new ValidationAppException("pageSize must be greater than or equal to 1.");
        if (pageSize.HasValue && pageSize.Value > MaxPageSize) throw new ValidationAppException("pageSize cannot be greater than 100.");
        return new ResolutionReportExportQuery(Normalize(userId), Normalize(incidentId), Normalize(emergencyResolutionReportId), ParseExportType(exportType), Parse<ResolutionReportExportStatus>(status, nameof(status)), dateFrom?.ToUniversalTime(), dateTo?.ToUniversalTime(), pageNumber ?? 1, pageSize ?? 20);
    }

    public ResolutionReportExportType ValidateExportType(string? exportType) => ParseExportType(exportType) ?? ResolutionReportExportType.Json;
    private static ResolutionReportExportType? ParseExportType(string? value) { if (string.IsNullOrWhiteSpace(value)) return null; if (string.Equals(value.Trim(), ResolutionReportExportType.Json.ToString(), StringComparison.Ordinal)) return ResolutionReportExportType.Json; throw new ValidationAppException("exportType is invalid."); }
    private static TEnum? Parse<TEnum>(string? value, string name) where TEnum : struct, Enum { if (string.IsNullOrWhiteSpace(value)) return null; if (!Enum.TryParse(value.Trim(), false, out TEnum parsed)) throw new ValidationAppException($"{name} is invalid."); return parsed; }
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
