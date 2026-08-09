using System.Globalization;
using System.Security.Claims;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Common.Results;
using MotoSOS.API.Modules.ReportExports.Application;
using MotoSOS.API.Modules.ReportExports.Contracts;
using MotoSOS.API.Modules.ReportExports.Domain;

namespace MotoSOS.API.Modules.ReportExports.Endpoints;

public static class ResolutionReportExportEndpoints
{
    public static IEndpointRouteBuilder MapResolutionReportExportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/rider/emergencies/{incidentId}/resolution-report/export", async (string incidentId, string? exportType, ClaimsPrincipal principal, ResolutionReportExportQueryValidator validator, IResolutionReportExportService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); ResolutionReportExportType type = validator.ValidateExportType(exportType); return Results.Ok(ApiResponse<ResolutionReportExportResponse>.Ok(await service.ExportForRiderAsync(userId, incidentId, type, ct))); }).RequireAuthorization().WithTags("ResolutionReportExports");
        endpoints.MapGet("/api/v1/admin/emergencies/{incidentId}/resolution-report/export", async (string incidentId, string? exportType, ClaimsPrincipal principal, ResolutionReportExportQueryValidator validator, IResolutionReportExportService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); ResolutionReportExportType type = validator.ValidateExportType(exportType); return Results.Ok(ApiResponse<ResolutionReportExportResponse>.Ok(await service.ExportForAdminAsync(userId, incidentId, type, ct))); }).RequireAuthorization().WithTags("ResolutionReportExports");
        endpoints.MapGet("/api/v1/monitor/alerts/{notificationDeliveryAttemptId}/resolution-report/export", async (string notificationDeliveryAttemptId, string? exportType, ClaimsPrincipal principal, ResolutionReportExportQueryValidator validator, IResolutionReportExportService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); ResolutionReportExportType type = validator.ValidateExportType(exportType); return Results.Ok(ApiResponse<ResolutionReportExportResponse>.Ok(await service.ExportForMonitorAsync(userId, notificationDeliveryAttemptId, type, ct))); }).RequireAuthorization().WithTags("ResolutionReportExports");
        endpoints.MapGet("/api/v1/admin/resolution-report-exports", async (string? userId, string? incidentId, string? emergencyResolutionReportId, string? exportType, string? status, string? dateFrom, string? dateTo, int? pageNumber, int? pageSize, ClaimsPrincipal principal, ResolutionReportExportQueryValidator validator, IResolutionReportExportService service, CancellationToken ct) => { string? adminUserId = GetUserId(principal); if (string.IsNullOrWhiteSpace(adminUserId)) return Results.Unauthorized(); ResolutionReportExportQuery query = validator.Validate(userId, incidentId, emergencyResolutionReportId, exportType, status, ParseDate(dateFrom, nameof(dateFrom)), ParseDate(dateTo, nameof(dateTo)), pageNumber, pageSize); return Results.Ok(ApiResponse<GetResolutionReportExportsResponse>.Ok(await service.ListForAdminAsync(adminUserId, query, ct))); }).RequireAuthorization().WithTags("ResolutionReportExports");
        return endpoints;
    }

    private static DateTimeOffset? ParseDate(string? value, string name) { if (string.IsNullOrWhiteSpace(value)) return null; if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset parsed)) throw new ValidationAppException($"{name} must be a valid ISO UTC date."); return parsed.ToUniversalTime(); }
    private static string? GetUserId(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
}
