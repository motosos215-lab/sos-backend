using System.Security.Claims;
using FluentValidation;
using MotoSOS.API.Common.Results;
using MotoSOS.API.Modules.Profiles.Application;
using MotoSOS.API.Modules.Profiles.Contracts;

namespace MotoSOS.API.Modules.Profiles.Endpoints;

public static class ProfileEndpoints
{
    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/profiles")
            .RequireAuthorization()
            .WithTags("Profiles");

        group.MapGet("/me", async (
            ClaimsPrincipal principal,
            IProfileService profileService,
            CancellationToken cancellationToken) =>
        {
            string? userId = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            GetMyProfileResponse response = await profileService.GetMyProfileAsync(userId, cancellationToken);
            return Results.Ok(ApiResponse<GetMyProfileResponse>.Ok(response));
        });

        group.MapPut("/me", async (
            UpsertMyProfileRequest request,
            IValidator<UpsertMyProfileRequest> validator,
            ClaimsPrincipal principal,
            IProfileService profileService,
            CancellationToken cancellationToken) =>
        {
            string? userId = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            var validation = await validator.ValidateAsync(request, cancellationToken);

            if (!validation.IsValid)
            {
                return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage)));
            }

            UpsertMyProfileResponse response = await profileService.UpsertMyProfileAsync(userId, request, cancellationToken);
            return Results.Ok(ApiResponse<UpsertMyProfileResponse>.Ok(response));
        });

        return endpoints;
    }
}
