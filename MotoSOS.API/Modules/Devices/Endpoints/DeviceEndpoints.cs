using System.Security.Claims;
using FluentValidation;
using MotoSOS.API.Common.Results;
using MotoSOS.API.Modules.Devices.Application;
using MotoSOS.API.Modules.Devices.Contracts;

namespace MotoSOS.API.Modules.Devices.Endpoints;

public static class DeviceEndpoints
{
    public static IEndpointRouteBuilder MapDeviceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/devices")
            .RequireAuthorization()
            .WithTags("Devices");

        group.MapGet(string.Empty, async (ClaimsPrincipal principal, IDeviceService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            return Results.Ok(ApiResponse<GetDevicesResponse>.Ok(await service.GetMyDevicesAsync(userId, cancellationToken)));
        });

        group.MapPost("/mobile/activation-code", async (ClaimsPrincipal principal, IDeviceService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            return Results.Ok(ApiResponse<CreateMobileActivationCodeResponse>.Ok(await service.CreateMobileActivationCodeAsync(userId, cancellationToken)));
        });

        group.MapGet("/activation-codes/current", async (ClaimsPrincipal principal, IDeviceService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            return Results.Ok(ApiResponse<GetCurrentMobileActivationCodeResponse>.Ok(await service.GetCurrentMobileActivationCodeAsync(userId, cancellationToken)));
        });

        group.MapPost("/mobile/link", async (LinkMobileDeviceRequest request, IValidator<LinkMobileDeviceRequest> validator, ClaimsPrincipal principal, IDeviceService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid) return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage)));
            return Results.Ok(ApiResponse<LinkMobileDeviceResponse>.Ok(await service.LinkMobileDeviceAsync(userId, request, cancellationToken)));
        });

        group.MapPost("/smartwatch/link", async (LinkSmartwatchRequest request, IValidator<LinkSmartwatchRequest> validator, ClaimsPrincipal principal, IDeviceService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid) return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage)));
            return Results.Ok(ApiResponse<LinkSmartwatchResponse>.Ok(await service.LinkSmartwatchAsync(userId, request, cancellationToken)));
        });

        group.MapPatch("/{id}/heartbeat", async (string id, HeartbeatDeviceRequest request, IValidator<HeartbeatDeviceRequest> validator, ClaimsPrincipal principal, IDeviceService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid) return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage)));
            return Results.Ok(ApiResponse<HeartbeatDeviceResponse>.Ok(await service.HeartbeatAsync(userId, id, request, cancellationToken)));
        });

        group.MapPost("/{id}/revoke", async (string id, ClaimsPrincipal principal, IDeviceService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            await service.RevokeAsync(userId, id, cancellationToken);
            return Results.NoContent();
        });

        return endpoints;
    }

    private static string? GetUserId(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
}
