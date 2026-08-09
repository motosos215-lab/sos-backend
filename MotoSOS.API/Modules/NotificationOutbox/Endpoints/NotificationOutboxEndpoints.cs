using System.Security.Claims;
using FluentValidation;
using MotoSOS.API.Common.Results;
using MotoSOS.API.Modules.NotificationOutbox.Application;
using MotoSOS.API.Modules.NotificationOutbox.Contracts;

namespace MotoSOS.API.Modules.NotificationOutbox.Endpoints;

public static class NotificationOutboxEndpoints
{
    public static IEndpointRouteBuilder MapNotificationOutboxEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/admin/notifications/outbox").RequireAuthorization().WithTags("NotificationOutbox");
        group.MapPost("/run", async (RunNotificationOutboxRequest request, IValidator<RunNotificationOutboxRequest> validator, ClaimsPrincipal principal, INotificationOutboxService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); var validation = await validator.ValidateAsync(request, ct); if (!validation.IsValid) return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage))); return Results.Ok(ApiResponse<RunNotificationOutboxResponse>.Ok(await service.RunAsync(userId, request, ct))); });
        group.MapGet("/status", async (ClaimsPrincipal principal, INotificationOutboxService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); return Results.Ok(ApiResponse<GetNotificationOutboxStatusResponse>.Ok(await service.GetStatusAsync(userId, ct))); });
        group.MapGet("/worker/status", async (ClaimsPrincipal principal, INotificationOutboxService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); return Results.Ok(ApiResponse<NotificationOutboxWorkerStatusResponse>.Ok(await service.GetWorkerStatusAsync(userId, ct))); });
        group.MapPost("/retry-failed", async (RetryFailedNotificationOutboxRequest request, IValidator<RetryFailedNotificationOutboxRequest> validator, ClaimsPrincipal principal, INotificationOutboxService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); var validation = await validator.ValidateAsync(request, ct); if (!validation.IsValid) return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage))); return Results.Ok(ApiResponse<RetryFailedNotificationOutboxResponse>.Ok(await service.RetryFailedAsync(userId, request, ct))); });
        return endpoints;
    }

    private static string? GetUserId(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
}
