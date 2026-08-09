using System.Security.Claims;
using FluentValidation;
using MotoSOS.API.Common.Results;
using MotoSOS.API.Modules.EmergencyResolution.Application;
using MotoSOS.API.Modules.EmergencyResolution.Contracts;

namespace MotoSOS.API.Modules.EmergencyResolution.Endpoints;

public static class EmergencyResolutionEndpoints
{
    public static IEndpointRouteBuilder MapEmergencyResolutionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder rider = endpoints.MapGroup("/api/v1/rider/emergencies").RequireAuthorization().WithTags("EmergencyResolution");
        rider.MapPost("/{incidentId}/resolution-report", async (string incidentId, CreateEmergencyResolutionReportRequest request, IValidator<CreateEmergencyResolutionReportRequest> validator, ClaimsPrincipal principal, IEmergencyResolutionService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); var validation = await validator.ValidateAsync(request, ct); if (!validation.IsValid) return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage))); return Results.Ok(ApiResponse<CreateEmergencyResolutionReportResponse>.Ok(await service.CreateForRiderAsync(userId, incidentId, request, ct))); });
        rider.MapGet("/{incidentId}/resolution-report", async (string incidentId, ClaimsPrincipal principal, IEmergencyResolutionService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); return Results.Ok(ApiResponse<GetEmergencyResolutionReportResponse>.Ok(await service.GetForRiderAsync(userId, incidentId, ct))); });
        rider.MapGet("/resolution-reports", async (string? outcome, int? pageNumber, int? pageSize, ClaimsPrincipal principal, IEmergencyResolutionService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); return Results.Ok(ApiResponse<GetEmergencyResolutionReportsResponse>.Ok(await service.ListForRiderAsync(userId, outcome, pageNumber, pageSize, ct))); });

        endpoints.MapGet("/api/v1/monitor/alerts/{notificationDeliveryAttemptId}/resolution-report", async (string notificationDeliveryAttemptId, ClaimsPrincipal principal, IEmergencyResolutionService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); return Results.Ok(ApiResponse<GetEmergencyResolutionReportResponse>.Ok(await service.GetForMonitorAsync(userId, notificationDeliveryAttemptId, ct))); }).RequireAuthorization().WithTags("EmergencyResolution");
        return endpoints;
    }

    private static string? GetUserId(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
}
