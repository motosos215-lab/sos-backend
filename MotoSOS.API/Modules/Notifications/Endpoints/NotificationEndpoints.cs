using System.Security.Claims;
using FluentValidation;
using MotoSOS.API.Common.Results;
using MotoSOS.API.Modules.Notifications.Application;
using MotoSOS.API.Modules.Notifications.Contracts;

namespace MotoSOS.API.Modules.Notifications.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/notifications/delivery-attempts").RequireAuthorization().WithTags("Notifications");

        group.MapPost("/prepare", async (PrepareNotificationAttemptsRequest request, IValidator<PrepareNotificationAttemptsRequest> validator, ClaimsPrincipal principal, INotificationService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            var validation = await validator.ValidateAsync(request, cancellationToken); if (!validation.IsValid) return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage)));
            return Results.Ok(ApiResponse<PrepareNotificationAttemptsResponse>.Ok(await service.PrepareAsync(userId, request, cancellationToken)));
        });

        group.MapGet(string.Empty, async (string? alertDispatchId, string? incidentId, string? status, int? pageNumber, int? pageSize, ClaimsPrincipal principal, INotificationService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            return Results.Ok(ApiResponse<GetNotificationDeliveryAttemptsResponse>.Ok(await service.ListAsync(userId, alertDispatchId, incidentId, status, pageNumber, pageSize, cancellationToken)));
        });

        group.MapGet("/{id}", async (string id, ClaimsPrincipal principal, INotificationService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            return Results.Ok(ApiResponse<GetNotificationDeliveryAttemptResponse>.Ok(await service.GetAsync(userId, id, cancellationToken)));
        });

        group.MapPost("/{id}/mark-simulated-sent", async (string id, MarkNotificationSimulatedSentRequest request, IValidator<MarkNotificationSimulatedSentRequest> validator, ClaimsPrincipal principal, INotificationService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            var validation = await validator.ValidateAsync(request, cancellationToken); if (!validation.IsValid) return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage)));
            return Results.Ok(ApiResponse<MarkNotificationSimulatedSentResponse>.Ok(await service.MarkSimulatedSentAsync(userId, id, request, cancellationToken)));
        });

        group.MapPost("/{id}/mark-failed", async (string id, MarkNotificationFailedRequest request, IValidator<MarkNotificationFailedRequest> validator, ClaimsPrincipal principal, INotificationService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            var validation = await validator.ValidateAsync(request, cancellationToken); if (!validation.IsValid) return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage)));
            return Results.Ok(ApiResponse<MarkNotificationFailedResponse>.Ok(await service.MarkFailedAsync(userId, id, request, cancellationToken)));
        });

        group.MapPost("/{id}/cancel", async (string id, CancelNotificationAttemptRequest request, IValidator<CancelNotificationAttemptRequest> validator, ClaimsPrincipal principal, INotificationService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            var validation = await validator.ValidateAsync(request, cancellationToken); if (!validation.IsValid) return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage)));
            return Results.Ok(ApiResponse<CancelNotificationAttemptResponse>.Ok(await service.CancelAsync(userId, id, request, cancellationToken)));
        });

        return endpoints;
    }

    private static string? GetUserId(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
}
