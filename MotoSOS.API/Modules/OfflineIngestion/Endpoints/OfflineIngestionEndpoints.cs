using System.Security.Claims;
using FluentValidation;
using MotoSOS.API.Common.Results;
using MotoSOS.API.Modules.OfflineIngestion.Application;
using MotoSOS.API.Modules.OfflineIngestion.Contracts;

namespace MotoSOS.API.Modules.OfflineIngestion.Endpoints;

public static class OfflineIngestionEndpoints
{
    private const long MaxBatchBytes = 256 * 1024;

    public static IEndpointRouteBuilder MapOfflineIngestionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/mobile/offline-ingestion")
            .RequireAuthorization()
            .WithTags("OfflineIngestion");

        group.MapPost("/batch", async (OfflineIngestionBatchRequest request, IValidator<OfflineIngestionBatchRequest> validator, ClaimsPrincipal principal, HttpRequest httpRequest, IOfflineIngestionService service, CancellationToken cancellationToken) =>
        {
            string? userId = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            if (httpRequest.ContentLength > MaxBatchBytes) return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", "Batch payload is too large.")));

            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid) return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage)));

            return Results.Ok(ApiResponse<OfflineIngestionBatchResponse>.Ok(await service.IngestBatchAsync(userId, request, cancellationToken)));
        });

        return endpoints;
    }
}
