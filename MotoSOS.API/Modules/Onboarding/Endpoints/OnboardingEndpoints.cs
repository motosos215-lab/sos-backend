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

        group.MapGet("/summary", async (
            ClaimsPrincipal principal,
            IOnboardingSummaryService summaryService,
            CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();

            OnboardingSummaryResponse response = await summaryService.GetSummaryAsync(userId, cancellationToken);
            return Results.Ok(ApiResponse<OnboardingSummaryResponse>.Ok(response));
        });

        group.MapPost("/confirm", async (
            ClaimsPrincipal principal,
            IOnboardingConfirmationService confirmationService,
            CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();

            ConfirmOnboardingResponse response = await confirmationService.ConfirmAsync(userId, cancellationToken);
            return Results.Ok(ApiResponse<ConfirmOnboardingResponse>.Ok(response));
        });

        return endpoints;
    }

    private static string? GetUserId(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
}
