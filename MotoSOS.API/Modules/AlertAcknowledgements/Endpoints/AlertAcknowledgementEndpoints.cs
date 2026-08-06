using System.Security.Claims;
using FluentValidation;
using MotoSOS.API.Common.Results;
using MotoSOS.API.Modules.AlertAcknowledgements.Application;
using MotoSOS.API.Modules.AlertAcknowledgements.Contracts;

namespace MotoSOS.API.Modules.AlertAcknowledgements.Endpoints;

public static class AlertAcknowledgementEndpoints
{
    public static IEndpointRouteBuilder MapAlertAcknowledgementEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder monitor = endpoints.MapGroup("/api/v1/monitor/alerts").RequireAuthorization().WithTags("AlertAcknowledgements");
        monitor.MapGet(string.Empty, async (string? status, int? pageNumber, int? pageSize, ClaimsPrincipal principal, IAlertAcknowledgementService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); return Results.Ok(ApiResponse<GetMonitorAlertsResponse>.Ok(await service.ListMonitorAlertsAsync(userId, status, pageNumber, pageSize, ct))); });
        monitor.MapGet("/{id}", async (string id, ClaimsPrincipal principal, IAlertAcknowledgementService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); return Results.Ok(ApiResponse<ViewAlertResponse>.Ok(await service.GetMonitorAlertAsync(userId, id, ct))); });
        monitor.MapPost("/{id}/view", async (string id, ClaimsPrincipal principal, IAlertAcknowledgementService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); return Results.Ok(ApiResponse<ViewAlertResponse>.Ok(await service.ViewAsync(userId, id, ct))); });
        monitor.MapPost("/{id}/acknowledge", async (string id, AcknowledgeAlertRequest request, IValidator<AcknowledgeAlertRequest> validator, ClaimsPrincipal principal, IAlertAcknowledgementService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); var validation = await validator.ValidateAsync(request, ct); if (!validation.IsValid) return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage))); return Results.Ok(ApiResponse<AcknowledgeAlertResponse>.Ok(await service.AcknowledgeAsync(userId, id, request, ct))); });
        monitor.MapPost("/{id}/decline", async (string id, DeclineAlertRequest request, IValidator<DeclineAlertRequest> validator, ClaimsPrincipal principal, IAlertAcknowledgementService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); var validation = await validator.ValidateAsync(request, ct); if (!validation.IsValid) return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage))); return Results.Ok(ApiResponse<DeclineAlertResponse>.Ok(await service.DeclineAsync(userId, id, request, ct))); });

        RouteGroupBuilder rider = endpoints.MapGroup("/api/v1/rider/alerts/acknowledgements").RequireAuthorization().WithTags("AlertAcknowledgements");
        rider.MapGet(string.Empty, async (string? alertDispatchId, string? incidentId, string? status, int? pageNumber, int? pageSize, ClaimsPrincipal principal, IAlertAcknowledgementService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); return Results.Ok(ApiResponse<GetAlertAcknowledgementsResponse>.Ok(await service.ListRiderAcknowledgementsAsync(userId, alertDispatchId, incidentId, status, pageNumber, pageSize, ct))); });
        return endpoints;
    }

    private static string? GetUserId(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
}
