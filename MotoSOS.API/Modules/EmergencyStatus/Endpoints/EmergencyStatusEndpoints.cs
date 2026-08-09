using System.Security.Claims;
using MotoSOS.API.Common.Results;
using MotoSOS.API.Modules.EmergencyStatus.Application;
using MotoSOS.API.Modules.EmergencyStatus.Contracts;

namespace MotoSOS.API.Modules.EmergencyStatus.Endpoints;

public static class EmergencyStatusEndpoints
{
    public static IEndpointRouteBuilder MapEmergencyStatusEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder rider = endpoints.MapGroup("/api/v1/rider/emergencies").RequireAuthorization().WithTags("EmergencyStatus");
        rider.MapGet("/active", async (int? pageNumber, int? pageSize, ClaimsPrincipal principal, IEmergencyStatusService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); return Results.Ok(ApiResponse<GetActiveEmergenciesResponse>.Ok(await service.ListActiveForRiderAsync(userId, pageNumber, pageSize, ct))); });
        rider.MapGet("/{incidentId}/status", async (string incidentId, ClaimsPrincipal principal, IEmergencyStatusService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); return Results.Ok(ApiResponse<EmergencyStatusResponse>.Ok(await service.GetForRiderAsync(userId, incidentId, ct))); });

        endpoints.MapGet("/api/v1/monitor/alerts/{notificationDeliveryAttemptId}/status", async (string notificationDeliveryAttemptId, ClaimsPrincipal principal, IEmergencyStatusService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); return Results.Ok(ApiResponse<EmergencyStatusResponse>.Ok(await service.GetForMonitorAsync(userId, notificationDeliveryAttemptId, ct))); }).RequireAuthorization().WithTags("EmergencyStatus");
        return endpoints;
    }

    private static string? GetUserId(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
}
