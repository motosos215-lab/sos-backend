using System.Globalization;
using System.Security.Claims;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Common.Results;
using MotoSOS.API.Modules.AuditLogs.Application;
using MotoSOS.API.Modules.AuditLogs.Contracts;

namespace MotoSOS.API.Modules.AuditLogs.Endpoints;

public static class AuditLogEndpoints
{
    public static IEndpointRouteBuilder MapAuditLogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/admin/audit-logs").RequireAuthorization().WithTags("AuditLogs");

        group.MapGet(string.Empty, async (
            string? actorUserId,
            string? action,
            string? module,
            string? outcome,
            string? entityType,
            string? entityId,
            string? dateFrom,
            string? dateTo,
            int? pageNumber,
            int? pageSize,
            ClaimsPrincipal principal,
            AuditLogQueryValidator validator,
            IAuditLogService service,
            CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            AuditLogQuery query = validator.Validate(actorUserId, action, module, outcome, entityType, entityId, ParseDate(dateFrom, nameof(dateFrom)), ParseDate(dateTo, nameof(dateTo)), pageNumber, pageSize);
            GetAuditLogsResponse response = await service.ListAsync(userId, query, cancellationToken);
            return Results.Ok(ApiResponse<GetAuditLogsResponse>.Ok(response));
        });

        group.MapGet("/{id}", async (string id, ClaimsPrincipal principal, IAuditLogService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            AuditLogResponse response = await service.GetAsync(userId, id, cancellationToken);
            return Results.Ok(ApiResponse<AuditLogResponse>.Ok(response));
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
