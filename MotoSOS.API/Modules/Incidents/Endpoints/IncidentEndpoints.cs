using System.Security.Claims;
using FluentValidation;
using MotoSOS.API.Common.Results;
using MotoSOS.API.Modules.Incidents.Application;
using MotoSOS.API.Modules.Incidents.Contracts;

namespace MotoSOS.API.Modules.Incidents.Endpoints;

public static class IncidentEndpoints
{
    public static IEndpointRouteBuilder MapIncidentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/incidents").RequireAuthorization().WithTags("Incidents");

        group.MapPost(string.Empty, async (CreateIncidentRequest request, IValidator<CreateIncidentRequest> validator, ClaimsPrincipal principal, IIncidentService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            var validation = await validator.ValidateAsync(request, cancellationToken); if (!validation.IsValid) return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage)));
            return Results.Ok(ApiResponse<CreateIncidentResponse>.Ok(await service.CreateAsync(userId, request, cancellationToken)));
        });

        group.MapGet(string.Empty, async (string? status, string? tripId, int? pageNumber, int? pageSize, ClaimsPrincipal principal, IIncidentService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            return Results.Ok(ApiResponse<GetIncidentsResponse>.Ok(await service.ListAsync(userId, status, tripId, pageNumber, pageSize, cancellationToken)));
        });

        group.MapGet("/{id}", async (string id, ClaimsPrincipal principal, IIncidentService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            return Results.Ok(ApiResponse<GetIncidentResponse>.Ok(await service.GetAsync(userId, id, cancellationToken)));
        });

        group.MapPost("/{id}/cancel-false-positive", async (string id, CancelFalsePositiveRequest request, IValidator<CancelFalsePositiveRequest> validator, ClaimsPrincipal principal, IIncidentService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            var validation = await validator.ValidateAsync(request, cancellationToken); if (!validation.IsValid) return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage)));
            return Results.Ok(ApiResponse<CancelFalsePositiveResponse>.Ok(await service.CancelFalsePositiveAsync(userId, id, request, cancellationToken)));
        });

        group.MapPost("/{id}/close", async (string id, CloseIncidentRequest request, IValidator<CloseIncidentRequest> validator, ClaimsPrincipal principal, IIncidentService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            var validation = await validator.ValidateAsync(request, cancellationToken); if (!validation.IsValid) return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage)));
            return Results.Ok(ApiResponse<CloseIncidentResponse>.Ok(await service.CloseAsync(userId, id, request, cancellationToken)));
        });

        return endpoints;
    }

    private static string? GetUserId(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
}
