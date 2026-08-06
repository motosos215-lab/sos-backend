using System.Security.Claims;
using FluentValidation;
using MotoSOS.API.Common.Results;
using MotoSOS.API.Modules.LocationSharing.Application;
using MotoSOS.API.Modules.LocationSharing.Contracts;

namespace MotoSOS.API.Modules.LocationSharing.Endpoints;

public static class LocationSharingEndpoints
{
    public static IEndpointRouteBuilder MapLocationSharingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/mobile/location-sharing/snapshot", async (ShareLocationSnapshotRequest request, IValidator<ShareLocationSnapshotRequest> validator, ClaimsPrincipal principal, ILocationSharingService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); var validation = await validator.ValidateAsync(request, ct); if (!validation.IsValid) return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage))); return Results.Ok(ApiResponse<ShareLocationSnapshotResponse>.Ok(await service.ShareAsync(userId, request, ct))); }).RequireAuthorization().WithTags("LocationSharing");
        endpoints.MapGet("/api/v1/monitor/alerts/{notificationDeliveryAttemptId}/location", async (string notificationDeliveryAttemptId, ClaimsPrincipal principal, ILocationSharingService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); return Results.Ok(ApiResponse<GetLocationSnapshotResponse>.Ok(await service.GetForMonitorAsync(userId, notificationDeliveryAttemptId, ct))); }).RequireAuthorization().WithTags("LocationSharing");
        endpoints.MapGet("/api/v1/rider/incidents/{incidentId}/location", async (string incidentId, ClaimsPrincipal principal, ILocationSharingService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); return Results.Ok(ApiResponse<GetLocationSnapshotResponse>.Ok(await service.GetForRiderAsync(userId, incidentId, ct))); }).RequireAuthorization().WithTags("LocationSharing");
        return endpoints;
    }
    private static string? GetUserId(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
}
