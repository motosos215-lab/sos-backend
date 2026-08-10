using System.Globalization;
using System.Security.Claims;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Common.Results;
using MotoSOS.API.Modules.PushNotificationTokens.Application;
using MotoSOS.API.Modules.PushNotificationTokens.Contracts;

namespace MotoSOS.API.Modules.PushNotificationTokens.Endpoints;

public static class PushNotificationTokenEndpoints
{
    public static IEndpointRouteBuilder MapPushNotificationTokenEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/push-notification-tokens").RequireAuthorization().WithTags("PushNotificationTokens");

        group.MapPost(string.Empty, async (RegisterPushNotificationTokenRequest request, ClaimsPrincipal principal, RegisterPushNotificationTokenRequestValidator validator, IPushNotificationTokenService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            ValidatedRegisterPushNotificationTokenRequest validated = validator.Validate(request);
            return Results.Ok(ApiResponse<RegisterPushNotificationTokenResponse>.Ok(await service.RegisterAsync(userId, validated, cancellationToken)));
        });

        group.MapGet(string.Empty, async (string? platform, string? channel, string? status, int? pageNumber, int? pageSize, ClaimsPrincipal principal, PushNotificationTokenQueryValidator validator, IPushNotificationTokenService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            PushNotificationTokenQuery query = validator.Validate(null, platform, channel, status, null, null, pageNumber, pageSize);
            return Results.Ok(ApiResponse<GetPushNotificationTokensResponse>.Ok(await service.ListMineAsync(userId, query, cancellationToken)));
        });

        group.MapGet("/status", async (ClaimsPrincipal principal, IPushNotificationTokenService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            return Results.Ok(ApiResponse<PushNotificationTokenStatusResponse>.Ok(await service.GetStatusAsync(userId, cancellationToken)));
        });

        group.MapPost("/{id}/revoke", async (string id, ClaimsPrincipal principal, IPushNotificationTokenService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            return Results.Ok(ApiResponse<PushNotificationTokenResponse>.Ok(await service.RevokeMineAsync(userId, id, cancellationToken)));
        });

        RouteGroupBuilder admin = endpoints.MapGroup("/api/v1/admin/push-notification-tokens").RequireAuthorization().WithTags("PushNotificationTokens");

        admin.MapGet(string.Empty, async (string? userId, string? platform, string? channel, string? status, string? dateFrom, string? dateTo, int? pageNumber, int? pageSize, ClaimsPrincipal principal, PushNotificationTokenQueryValidator validator, IPushNotificationTokenService service, CancellationToken cancellationToken) =>
        {
            string? adminUserId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(adminUserId)) return Results.Unauthorized();
            PushNotificationTokenQuery query = validator.Validate(userId, platform, channel, status, ParseDate(dateFrom, nameof(dateFrom)), ParseDate(dateTo, nameof(dateTo)), pageNumber, pageSize);
            return Results.Ok(ApiResponse<GetPushNotificationTokensResponse>.Ok(await service.ListForAdminAsync(adminUserId, query, cancellationToken)));
        });

        admin.MapPost("/{id}/revoke", async (string id, ClaimsPrincipal principal, IPushNotificationTokenService service, CancellationToken cancellationToken) =>
        {
            string? adminUserId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(adminUserId)) return Results.Unauthorized();
            return Results.Ok(ApiResponse<PushNotificationTokenResponse>.Ok(await service.RevokeForAdminAsync(adminUserId, id, cancellationToken)));
        });

        return endpoints;
    }

    private static DateTimeOffset? ParseDate(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset parsed)) throw new ValidationAppException($"{name} must be a valid ISO UTC date.");
        return parsed.ToUniversalTime();
    }

    private static string? GetUserId(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
}
