using System.Security.Claims;
using FluentValidation;
using MotoSOS.API.Common.Results;
using MotoSOS.API.Modules.NotificationPreferences.Application;
using MotoSOS.API.Modules.NotificationPreferences.Contracts;

namespace MotoSOS.API.Modules.NotificationPreferences.Endpoints;

public static class NotificationPreferenceEndpoints
{
    public static IEndpointRouteBuilder MapNotificationPreferenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/notification-preferences").RequireAuthorization().WithTags("NotificationPreferences");

        group.MapGet("/me", async (ClaimsPrincipal principal, INotificationPreferenceService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            return Results.Ok(ApiResponse<GetNotificationPreferenceResponse>.Ok(await service.GetMineAsync(userId, cancellationToken)));
        });

        group.MapPut("/me", async (UpdateNotificationPreferenceRequest request, IValidator<UpdateNotificationPreferenceRequest> validator, ClaimsPrincipal principal, INotificationPreferenceService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid) return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage)));
            return Results.Ok(ApiResponse<UpdateNotificationPreferenceResponse>.Ok(await service.UpdateMineAsync(userId, request, cancellationToken)));
        });

        return endpoints;
    }

    private static string? GetUserId(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
}
