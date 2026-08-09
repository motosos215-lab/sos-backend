using System.Globalization;
using System.Security.Claims;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Common.Results;
using MotoSOS.API.Modules.TelemetrySummary.Application;
using MotoSOS.API.Modules.TelemetrySummary.Contracts;

namespace MotoSOS.API.Modules.TelemetrySummary.Endpoints;

public static class TelemetrySummaryEndpoints
{
    public static IEndpointRouteBuilder MapTelemetrySummaryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder rider = endpoints.MapGroup("/api/v1/rider/trips/{tripId}/telemetry-summary").RequireAuthorization().WithTags("TelemetrySummary");
        rider.MapGet("", async (string tripId, ClaimsPrincipal principal, ITelemetrySummaryService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); return Results.Ok(ApiResponse<TelemetrySummaryResponse>.Ok(await service.GetForRiderAsync(userId, tripId, ct))); });
        rider.MapPost("/recompute", async (string tripId, ClaimsPrincipal principal, ITelemetrySummaryService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); return Results.Ok(ApiResponse<TelemetrySummaryResponse>.Ok(await service.RecomputeForRiderAsync(userId, tripId, ct))); });

        RouteGroupBuilder admin = endpoints.MapGroup("/api/v1/admin/telemetry-summaries").RequireAuthorization().WithTags("TelemetrySummary");
        admin.MapGet("", async (string? userId, string? tripId, string? tripStatus, string? summaryStatus, string? dateFrom, string? dateTo, int? pageNumber, int? pageSize, ClaimsPrincipal principal, TelemetrySummaryQueryValidator validator, ITelemetrySummaryService service, CancellationToken ct) => { string? adminUserId = GetUserId(principal); if (string.IsNullOrWhiteSpace(adminUserId)) return Results.Unauthorized(); TelemetrySummaryQuery query = validator.Validate(userId, tripId, tripStatus, summaryStatus, ParseDate(dateFrom, nameof(dateFrom)), ParseDate(dateTo, nameof(dateTo)), pageNumber, pageSize); return Results.Ok(ApiResponse<GetTelemetrySummariesResponse>.Ok(await service.ListForAdminAsync(adminUserId, query, ct))); });
        admin.MapGet("/{id}", async (string id, ClaimsPrincipal principal, ITelemetrySummaryService service, CancellationToken ct) => { string? adminUserId = GetUserId(principal); if (string.IsNullOrWhiteSpace(adminUserId)) return Results.Unauthorized(); return Results.Ok(ApiResponse<TelemetrySummaryResponse>.Ok(await service.GetForAdminAsync(adminUserId, id, ct))); });
        return endpoints;
    }

    private static DateTimeOffset? ParseDate(string? value, string name) { if (string.IsNullOrWhiteSpace(value)) return null; if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset parsed)) throw new ValidationAppException($"{name} must be a valid ISO UTC date."); return parsed.ToUniversalTime(); }
    private static string? GetUserId(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
}
