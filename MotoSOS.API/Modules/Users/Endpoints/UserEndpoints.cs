using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using MotoSOS.API.Common.Results;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Contracts;

namespace MotoSOS.API.Modules.Users.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/users")
            .RequireAuthorization()
            .WithTags("Users");

        group.MapGet("/me", async (
            ClaimsPrincipal principal,
            IUserService userService,
            CancellationToken cancellationToken) =>
        {
            string? userId = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            CurrentUserResponse response = await userService.GetCurrentUserAsync(userId, cancellationToken);
            return Results.Ok(ApiResponse<CurrentUserResponse>.Ok(response));
        });

        return endpoints;
    }
}
