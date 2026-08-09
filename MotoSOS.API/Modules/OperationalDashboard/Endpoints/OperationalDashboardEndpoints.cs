using System.Globalization;
using System.Security.Claims;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Common.Results;
using MotoSOS.API.Modules.OperationalDashboard.Application;
using MotoSOS.API.Modules.OperationalDashboard.Contracts;

namespace MotoSOS.API.Modules.OperationalDashboard.Endpoints;

public static class OperationalDashboardEndpoints
{
    public static IEndpointRouteBuilder MapOperationalDashboardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/admin/dashboard").RequireAuthorization().WithTags("OperationalDashboard");
        group.MapGet("/summary", async (ClaimsPrincipal principal, IOperationalDashboardService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); return Results.Ok(ApiResponse<OperationalDashboardSummaryResponse>.Ok(await service.GetSummaryAsync(userId, ct))); });
        group.MapGet("/incidents", async (string? dateFrom, string? dateTo, string? status, int? pageNumber, int? pageSize, ClaimsPrincipal principal, OperationalDashboardQueryValidator validator, IOperationalDashboardService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); OperationalDashboardQuery query = validator.ValidateIncidentQuery(ParseDate(dateFrom, nameof(dateFrom)), ParseDate(dateTo, nameof(dateTo)), status, pageNumber, pageSize); return Results.Ok(ApiResponse<OperationalDashboardIncidentListResponse>.Ok(await service.ListIncidentsAsync(userId, query, ct))); });
        group.MapGet("/response-times", async (string? dateFrom, string? dateTo, ClaimsPrincipal principal, OperationalDashboardQueryValidator validator, IOperationalDashboardService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); OperationalDashboardQuery query = validator.ValidateDateRange(ParseDate(dateFrom, nameof(dateFrom)), ParseDate(dateTo, nameof(dateTo))); return Results.Ok(ApiResponse<OperationalDashboardResponseTimesResponse>.Ok(await service.GetResponseTimesAsync(userId, query, ct))); });
        group.MapGet("/resolution-outcomes", async (string? dateFrom, string? dateTo, ClaimsPrincipal principal, OperationalDashboardQueryValidator validator, IOperationalDashboardService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); OperationalDashboardQuery query = validator.ValidateDateRange(ParseDate(dateFrom, nameof(dateFrom)), ParseDate(dateTo, nameof(dateTo))); return Results.Ok(ApiResponse<OperationalDashboardResolutionOutcomesResponse>.Ok(await service.GetResolutionOutcomesAsync(userId, query, ct))); });
        group.MapGet("/offline-processing", async (ClaimsPrincipal principal, IOperationalDashboardService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); return Results.Ok(ApiResponse<OperationalDashboardOfflineProcessingSummaryResponse>.Ok(await service.GetOfflineProcessingAsync(userId, ct))); });
        return endpoints;
    }

    private static DateTimeOffset? ParseDate(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset parsed)) throw new ValidationAppException($"{name} must be a valid ISO UTC date.");
        return parsed.ToUniversalTime();
    }

    private static string? GetUserId(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
}
