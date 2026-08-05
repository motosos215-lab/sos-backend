using System.Security.Claims;
using FluentValidation;
using MotoSOS.API.Common.Results;
using MotoSOS.API.Modules.Vehicles.Application;
using MotoSOS.API.Modules.Vehicles.Contracts;

namespace MotoSOS.API.Modules.Vehicles.Endpoints;

public static class VehicleEndpoints
{
    public static IEndpointRouteBuilder MapVehicleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/vehicles")
            .RequireAuthorization()
            .WithTags("Vehicles");

        group.MapGet(string.Empty, async (ClaimsPrincipal principal, IVehicleService vehicleService, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            GetVehiclesResponse response = await vehicleService.GetMyVehiclesAsync(userId, cancellationToken);
            return Results.Ok(ApiResponse<GetVehiclesResponse>.Ok(response));
        });

        group.MapGet("/{id}", async (string id, ClaimsPrincipal principal, IVehicleService vehicleService, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            GetVehicleResponse response = await vehicleService.GetMyVehicleAsync(userId, id, cancellationToken);
            return Results.Ok(ApiResponse<GetVehicleResponse>.Ok(response));
        });

        group.MapPost(string.Empty, async (
            CreateVehicleRequest request,
            IValidator<CreateVehicleRequest> validator,
            ClaimsPrincipal principal,
            IVehicleService vehicleService,
            CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage)));
            }

            CreateVehicleResponse response = await vehicleService.CreateMyVehicleAsync(userId, request, cancellationToken);
            return Results.Created($"/api/v1/vehicles/{response.Vehicle.Id}", ApiResponse<CreateVehicleResponse>.Ok(response));
        });

        group.MapPut("/{id}", async (
            string id,
            UpdateVehicleRequest request,
            IValidator<UpdateVehicleRequest> validator,
            ClaimsPrincipal principal,
            IVehicleService vehicleService,
            CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage)));
            }

            UpdateVehicleResponse response = await vehicleService.UpdateMyVehicleAsync(userId, id, request, cancellationToken);
            return Results.Ok(ApiResponse<UpdateVehicleResponse>.Ok(response));
        });

        group.MapDelete("/{id}", async (string id, ClaimsPrincipal principal, IVehicleService vehicleService, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            await vehicleService.DeleteMyVehicleAsync(userId, id, cancellationToken);
            return Results.NoContent();
        });

        return endpoints;
    }

    private static string? GetUserId(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
}
