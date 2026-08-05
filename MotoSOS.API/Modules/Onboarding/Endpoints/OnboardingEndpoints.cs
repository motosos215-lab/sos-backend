using System.Security.Claims;
using MotoSOS.API.Common.Results;
using MotoSOS.API.Modules.Onboarding.Application;
using MotoSOS.API.Modules.Onboarding.Contracts;

namespace MotoSOS.API.Modules.Onboarding.Endpoints;

public static class OnboardingEndpoints
{
    public static IEndpointRouteBuilder MapOnboardingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/onboarding")
            .RequireAuthorization()
            .WithTags("Onboarding");

        group.MapGet("/status", async (
            ClaimsPrincipal principal,
            IOnboardingService onboardingService,
            CancellationToken cancellationToken) =>
        {
            string? userId = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            OnboardingStatusResponse response = await onboardingService.GetStatusAsync(userId, cancellationToken);
            return Results.Ok(ApiResponse<OnboardingStatusResponse>.Ok(response));
        });

        return endpoints;
    }
}
