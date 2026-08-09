using System.Security.Claims;
using FluentValidation;
using MotoSOS.API.Common.Results;
using MotoSOS.API.Modules.OfflineProcessing.Application;
using MotoSOS.API.Modules.OfflineProcessing.Contracts;

namespace MotoSOS.API.Modules.OfflineProcessing.Endpoints;

public static class OfflineProcessingEndpoints
{
    public static IEndpointRouteBuilder MapOfflineProcessingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/offline-processing").RequireAuthorization().WithTags("OfflineProcessing");
        group.MapPost("/run", async (RunOfflineProcessingRequest request, IValidator<RunOfflineProcessingRequest> validator, ClaimsPrincipal principal, IOfflineProcessingService service, CancellationToken ct) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            var validation = await validator.ValidateAsync(request, ct);
            if (!validation.IsValid) return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage)));
            return Results.Ok(ApiResponse<RunOfflineProcessingResponse>.Ok(await service.RunAsync(userId, request, ct)));
        });
        group.MapGet("/status", async (ClaimsPrincipal principal, IOfflineProcessingService service, CancellationToken ct) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            return Results.Ok(ApiResponse<GetOfflineProcessingStatusResponse>.Ok(await service.GetStatusAsync(userId, ct)));
        });
        return endpoints;
    }

    private static string? GetUserId(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
}
