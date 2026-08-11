using System.Security.Claims;
using FluentValidation;
using MotoSOS.API.Common.Results;
using MotoSOS.API.Modules.SosAlerts.Application;
using MotoSOS.API.Modules.SosAlerts.Contracts;

namespace MotoSOS.API.Modules.SosAlerts.Endpoints;

public static class SosAlertEndpoints
{
    public static IEndpointRouteBuilder MapSosAlertEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/mobile/sos-alerts").RequireAuthorization().WithTags("SosAlerts");

        group.MapPost(string.Empty, async (CreateSosAlertRequest request, IValidator<CreateSosAlertRequest> validator, ClaimsPrincipal principal, ICreateSosAlertService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            var validation = await validator.ValidateAsync(request, cancellationToken); if (!validation.IsValid) return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage)));
            return Results.Ok(ApiResponse<CreateSosAlertResponse>.Ok(await service.CreateAsync(userId, request, cancellationToken)));
        });

        return endpoints;
    }

    private static string? GetUserId(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
}
