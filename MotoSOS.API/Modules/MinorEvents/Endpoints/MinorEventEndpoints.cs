using System.Globalization;
using System.Security.Claims;
using FluentValidation;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Common.Results;
using MotoSOS.API.Modules.MinorEvents.Application;
using MotoSOS.API.Modules.MinorEvents.Contracts;

namespace MotoSOS.API.Modules.MinorEvents.Endpoints;

public static class MinorEventEndpoints
{
    public static IEndpointRouteBuilder MapMinorEventEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/mobile/minor-events", async (CreateMinorEventRequest request, IValidator<CreateMinorEventRequest> validator, ClaimsPrincipal principal, IMinorEventService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); var validation = await validator.ValidateAsync(request, ct); if (!validation.IsValid) return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage))); return Results.Ok(ApiResponse<CreateMinorEventResponse>.Ok(await service.CreateAsync(userId, request, ct))); }).RequireAuthorization().WithTags("MinorEvents");

        RouteGroupBuilder rider = endpoints.MapGroup("/api/v1/rider/minor-events").RequireAuthorization().WithTags("MinorEvents");
        rider.MapGet("", async (string? tripId, string? eventType, string? severity, string? status, string? dateFrom, string? dateTo, int? pageNumber, int? pageSize, ClaimsPrincipal principal, MinorEventQueryValidator validator, IMinorEventService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); MinorEventQuery query = validator.Validate(null, tripId, eventType, severity, status, ParseDate(dateFrom, nameof(dateFrom)), ParseDate(dateTo, nameof(dateTo)), pageNumber, pageSize); return Results.Ok(ApiResponse<GetMinorEventsResponse>.Ok(await service.ListForRiderAsync(userId, query, ct))); });
        rider.MapGet("/{id}", async (string id, ClaimsPrincipal principal, IMinorEventService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); return Results.Ok(ApiResponse<GetMinorEventResponse>.Ok(await service.GetForRiderAsync(userId, id, ct))); });
        rider.MapPost("/{id}/mark-reviewed", async (string id, ClaimsPrincipal principal, IMinorEventService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); return Results.Ok(ApiResponse<GetMinorEventResponse>.Ok(await service.MarkReviewedAsync(userId, id, ct))); });
        rider.MapPost("/{id}/ignore", async (string id, ClaimsPrincipal principal, IMinorEventService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); return Results.Ok(ApiResponse<GetMinorEventResponse>.Ok(await service.IgnoreAsync(userId, id, ct))); });

        endpoints.MapGet("/api/v1/admin/minor-events", async (string? userId, string? tripId, string? eventType, string? severity, string? status, string? dateFrom, string? dateTo, int? pageNumber, int? pageSize, ClaimsPrincipal principal, MinorEventQueryValidator validator, IMinorEventService service, CancellationToken ct) => { string? adminUserId = GetUserId(principal); if (string.IsNullOrWhiteSpace(adminUserId)) return Results.Unauthorized(); MinorEventQuery query = validator.Validate(userId, tripId, eventType, severity, status, ParseDate(dateFrom, nameof(dateFrom)), ParseDate(dateTo, nameof(dateTo)), pageNumber, pageSize); return Results.Ok(ApiResponse<GetMinorEventsResponse>.Ok(await service.ListForAdminAsync(adminUserId, query, ct))); }).RequireAuthorization().WithTags("MinorEvents");
        return endpoints;
    }

    private static DateTimeOffset? ParseDate(string? value, string name) { if (string.IsNullOrWhiteSpace(value)) return null; if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset parsed)) throw new ValidationAppException($"{name} must be a valid ISO UTC date."); return parsed.ToUniversalTime(); }
    private static string? GetUserId(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
}
