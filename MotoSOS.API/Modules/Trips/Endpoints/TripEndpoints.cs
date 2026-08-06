using System.Security.Claims;
using FluentValidation;
using MotoSOS.API.Common.Results;
using MotoSOS.API.Modules.Trips.Application;
using MotoSOS.API.Modules.Trips.Contracts;

namespace MotoSOS.API.Modules.Trips.Endpoints;

public static class TripEndpoints
{
    public static IEndpointRouteBuilder MapTripEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/trips")
            .RequireAuthorization()
            .WithTags("Trips");

        group.MapGet("/active", async (ClaimsPrincipal principal, ITripService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            return Results.Ok(ApiResponse<GetActiveTripResponse>.Ok(await service.GetActiveAsync(userId, cancellationToken)));
        });

        group.MapPost("/start", async (StartTripRequest request, IValidator<StartTripRequest> validator, ClaimsPrincipal principal, ITripService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid) return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage)));
            return Results.Ok(ApiResponse<StartTripResponse>.Ok(await service.StartAsync(userId, request, cancellationToken)));
        });

        group.MapPost("/{id}/finish", async (string id, FinishTripRequest request, IValidator<FinishTripRequest> validator, ClaimsPrincipal principal, ITripService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid) return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage)));
            return Results.Ok(ApiResponse<FinishTripResponse>.Ok(await service.FinishAsync(userId, id, request, cancellationToken)));
        });

        group.MapGet("/{id}", async (string id, ClaimsPrincipal principal, ITripService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            return Results.Ok(ApiResponse<GetTripResponse>.Ok(await service.GetAsync(userId, id, cancellationToken)));
        });

        group.MapGet(string.Empty, async (string? status, int? pageNumber, int? pageSize, ClaimsPrincipal principal, ITripService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            return Results.Ok(ApiResponse<GetTripsResponse>.Ok(await service.ListAsync(userId, status, pageNumber, pageSize, cancellationToken)));
        });

        return endpoints;
    }

    private static string? GetUserId(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
}
