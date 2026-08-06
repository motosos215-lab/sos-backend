using System.Security.Claims;
using MotoSOS.API.Common.Results;
using MotoSOS.API.Modules.Plans.Application;
using MotoSOS.API.Modules.Plans.Contracts;

namespace MotoSOS.API.Modules.Plans.Endpoints;

public static class PlanEndpoints
{
    public static IEndpointRouteBuilder MapPlanEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder plans = endpoints.MapGroup("/api/v1/plans")
            .RequireAuthorization()
            .WithTags("Plans");

        plans.MapGet(string.Empty, async (ClaimsPrincipal principal, ISubscriptionService subscriptionService, IPlanCatalogService planCatalog, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            await subscriptionService.GetMySubscriptionAsync(userId, cancellationToken);
            return Results.Ok(ApiResponse<GetPlansResponse>.Ok(planCatalog.GetPlans()));
        });

        RouteGroupBuilder subscriptions = endpoints.MapGroup("/api/v1/subscriptions")
            .RequireAuthorization()
            .WithTags("Subscriptions");

        subscriptions.MapGet("/me", async (ClaimsPrincipal principal, ISubscriptionService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            return Results.Ok(ApiResponse<GetMySubscriptionResponse>.Ok(await service.GetMySubscriptionAsync(userId, cancellationToken)));
        });

        subscriptions.MapPost("/select-basic", async (ClaimsPrincipal principal, ISubscriptionService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            return Results.Ok(ApiResponse<SelectBasicSubscriptionResponse>.Ok(await service.SelectBasicAsync(userId, cancellationToken)));
        });

        return endpoints;
    }

    private static string? GetUserId(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
}
