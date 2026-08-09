using System.Security.Claims;
using FluentValidation;
using MotoSOS.API.Common.Results;
using MotoSOS.API.Modules.AlertDispatch.Application;
using MotoSOS.API.Modules.AlertDispatch.Contracts;

namespace MotoSOS.API.Modules.AlertDispatch.Endpoints;

public static class AlertDispatchEndpoints
{
    public static IEndpointRouteBuilder MapAlertDispatchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/alert-dispatches").RequireAuthorization().WithTags("AlertDispatch");

        group.MapPost(string.Empty, async (CreateAlertDispatchRequest request, IValidator<CreateAlertDispatchRequest> validator, ClaimsPrincipal principal, IAlertDispatchService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            var validation = await validator.ValidateAsync(request, cancellationToken); if (!validation.IsValid) return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage)));
            return Results.Ok(ApiResponse<CreateAlertDispatchResponse>.Ok(await service.CreateAsync(userId, request, cancellationToken)));
        });

        group.MapGet(string.Empty, async (string? status, string? incidentId, int? pageNumber, int? pageSize, ClaimsPrincipal principal, IAlertDispatchService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            return Results.Ok(ApiResponse<GetAlertDispatchesResponse>.Ok(await service.ListAsync(userId, status, incidentId, pageNumber, pageSize, cancellationToken)));
        });

        group.MapGet("/{id}", async (string id, ClaimsPrincipal principal, IAlertDispatchService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            return Results.Ok(ApiResponse<GetAlertDispatchResponse>.Ok(await service.GetAsync(userId, id, cancellationToken)));
        });

        group.MapPost("/{id}/cancel", async (string id, CancelAlertDispatchRequest request, IValidator<CancelAlertDispatchRequest> validator, ClaimsPrincipal principal, IAlertDispatchService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            var validation = await validator.ValidateAsync(request, cancellationToken); if (!validation.IsValid) return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage)));
            return Results.Ok(ApiResponse<CancelAlertDispatchResponse>.Ok(await service.CancelAsync(userId, id, request, cancellationToken)));
        });

        return endpoints;
    }

    private static string? GetUserId(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
}
