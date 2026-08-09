using System.Security.Claims;
using MotoSOS.API.Common.Results;
using MotoSOS.API.Modules.AuditLogRetention.Application;
using MotoSOS.API.Modules.AuditLogRetention.Contracts;

namespace MotoSOS.API.Modules.AuditLogRetention.Endpoints;

public static class AuditLogRetentionEndpoints
{
    public static IEndpointRouteBuilder MapAuditLogRetentionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/admin/audit-logs/retention").RequireAuthorization().WithTags("AuditLogRetention");

        group.MapGet("/policy", async (ClaimsPrincipal principal, IAuditLogRetentionService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            AuditLogRetentionPolicyResponse response = await service.GetPolicyAsync(userId, cancellationToken);
            return Results.Ok(ApiResponse<AuditLogRetentionPolicyResponse>.Ok(response));
        });

        group.MapPost("/run", async (RunAuditLogRetentionRequest? request, ClaimsPrincipal principal, RunAuditLogRetentionRequestValidator validator, IAuditLogRetentionService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            ValidatedAuditLogRetentionRunRequest validated = validator.Validate(request);
            AuditLogRetentionRunResponse response = await service.RunAsync(userId, validated, cancellationToken);
            return Results.Ok(ApiResponse<AuditLogRetentionRunResponse>.Ok(response));
        });

        group.MapGet("/runs", async (int? pageNumber, int? pageSize, ClaimsPrincipal principal, AuditLogRetentionRunQueryValidator validator, IAuditLogRetentionService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            AuditLogRetentionRunQuery query = validator.Validate(pageNumber, pageSize);
            GetAuditLogRetentionRunsResponse response = await service.ListRunsAsync(userId, query, cancellationToken);
            return Results.Ok(ApiResponse<GetAuditLogRetentionRunsResponse>.Ok(response));
        });

        group.MapGet("/runs/{id}", async (string id, ClaimsPrincipal principal, IAuditLogRetentionService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            AuditLogRetentionRunResponse response = await service.GetRunAsync(userId, id, cancellationToken);
            return Results.Ok(ApiResponse<AuditLogRetentionRunResponse>.Ok(response));
        });

        return endpoints;
    }

    private static string? GetUserId(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
}
