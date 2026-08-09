using System.Globalization;
using System.Security.Claims;
using FluentValidation;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Common.Results;
using MotoSOS.API.Modules.EvidenceAttachments.Application;
using MotoSOS.API.Modules.EvidenceAttachments.Contracts;

namespace MotoSOS.API.Modules.EvidenceAttachments.Endpoints;

public static class EvidenceAttachmentEndpoints
{
    public static IEndpointRouteBuilder MapEvidenceAttachmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder rider = endpoints.MapGroup("/api/v1/rider/evidence-attachments").RequireAuthorization().WithTags("EvidenceAttachments");
        rider.MapPost("", async (CreateEvidenceAttachmentRequest request, IValidator<CreateEvidenceAttachmentRequest> validator, ClaimsPrincipal principal, IEvidenceAttachmentService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); var validation = await validator.ValidateAsync(request, ct); if (!validation.IsValid) return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage))); return Results.Ok(ApiResponse<CreateEvidenceAttachmentResponse>.Ok(await service.CreateForRiderAsync(userId, request, ct))); });
        rider.MapGet("", async (string? incidentId, string? alertDispatchId, string? emergencyResolutionReportId, string? evidenceType, string? source, string? status, string? dateFrom, string? dateTo, int? pageNumber, int? pageSize, ClaimsPrincipal principal, EvidenceAttachmentQueryValidator validator, IEvidenceAttachmentService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); EvidenceAttachmentQuery query = validator.Validate(null, incidentId, alertDispatchId, emergencyResolutionReportId, evidenceType, source, status, ParseDate(dateFrom, nameof(dateFrom)), ParseDate(dateTo, nameof(dateTo)), pageNumber, pageSize); return Results.Ok(ApiResponse<GetEvidenceAttachmentsResponse>.Ok(await service.ListForRiderAsync(userId, query, ct))); });
        rider.MapGet("/{id}", async (string id, ClaimsPrincipal principal, IEvidenceAttachmentService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); return Results.Ok(ApiResponse<EvidenceAttachmentResponse>.Ok(await service.GetForRiderAsync(userId, id, ct))); });
        rider.MapPost("/{id}/delete", async (string id, ClaimsPrincipal principal, IEvidenceAttachmentService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); return Results.Ok(ApiResponse<EvidenceAttachmentResponse>.Ok(await service.DeleteForRiderAsync(userId, id, ct))); });

        endpoints.MapPost("/api/v1/monitor/evidence-attachments", async (CreateEvidenceAttachmentRequest request, IValidator<CreateEvidenceAttachmentRequest> validator, ClaimsPrincipal principal, IEvidenceAttachmentService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); var validation = await validator.ValidateAsync(request, ct); if (!validation.IsValid) return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage))); return Results.Ok(ApiResponse<CreateEvidenceAttachmentResponse>.Ok(await service.CreateForMonitorAsync(userId, request, ct))); }).RequireAuthorization().WithTags("EvidenceAttachments");

        RouteGroupBuilder admin = endpoints.MapGroup("/api/v1/admin/evidence-attachments").RequireAuthorization().WithTags("EvidenceAttachments");
        admin.MapGet("", async (string? userId, string? incidentId, string? alertDispatchId, string? emergencyResolutionReportId, string? evidenceType, string? source, string? status, string? dateFrom, string? dateTo, int? pageNumber, int? pageSize, ClaimsPrincipal principal, EvidenceAttachmentQueryValidator validator, IEvidenceAttachmentService service, CancellationToken ct) => { string? adminUserId = GetUserId(principal); if (string.IsNullOrWhiteSpace(adminUserId)) return Results.Unauthorized(); EvidenceAttachmentQuery query = validator.Validate(userId, incidentId, alertDispatchId, emergencyResolutionReportId, evidenceType, source, status, ParseDate(dateFrom, nameof(dateFrom)), ParseDate(dateTo, nameof(dateTo)), pageNumber, pageSize); return Results.Ok(ApiResponse<GetEvidenceAttachmentsResponse>.Ok(await service.ListForAdminAsync(adminUserId, query, ct))); });
        admin.MapGet("/{id}", async (string id, ClaimsPrincipal principal, IEvidenceAttachmentService service, CancellationToken ct) => { string? adminUserId = GetUserId(principal); if (string.IsNullOrWhiteSpace(adminUserId)) return Results.Unauthorized(); return Results.Ok(ApiResponse<EvidenceAttachmentResponse>.Ok(await service.GetForAdminAsync(adminUserId, id, ct))); });
        return endpoints;
    }

    private static DateTimeOffset? ParseDate(string? value, string name) { if (string.IsNullOrWhiteSpace(value)) return null; if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset parsed)) throw new ValidationAppException($"{name} must be a valid ISO UTC date."); return parsed.ToUniversalTime(); }
    private static string? GetUserId(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
}
