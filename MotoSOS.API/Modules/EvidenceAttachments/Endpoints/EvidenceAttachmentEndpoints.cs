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
        rider.MapPost("upload", async (HttpRequest request, ClaimsPrincipal principal, IEvidenceAttachmentService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); EvidenceUploadCommand command = await ParseUploadAsync(request, ct); return Results.Ok(ApiResponse<UploadEvidenceAttachmentResponse>.Ok(await service.UploadForRiderAsync(userId, command, ct))); }).DisableAntiforgery();
        rider.MapGet("", async (string? incidentId, string? alertDispatchId, string? emergencyResolutionReportId, string? evidenceType, string? source, string? status, string? dateFrom, string? dateTo, int? pageNumber, int? pageSize, ClaimsPrincipal principal, EvidenceAttachmentQueryValidator validator, IEvidenceAttachmentService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); EvidenceAttachmentQuery query = validator.Validate(null, incidentId, alertDispatchId, emergencyResolutionReportId, evidenceType, source, status, ParseDate(dateFrom, nameof(dateFrom)), ParseDate(dateTo, nameof(dateTo)), pageNumber, pageSize); return Results.Ok(ApiResponse<GetEvidenceAttachmentsResponse>.Ok(await service.ListForRiderAsync(userId, query, ct))); });
        rider.MapGet("/{id}", async (string id, ClaimsPrincipal principal, IEvidenceAttachmentService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); return Results.Ok(ApiResponse<EvidenceAttachmentResponse>.Ok(await service.GetForRiderAsync(userId, id, ct))); });
        rider.MapGet("/{id}/download", async (string id, ClaimsPrincipal principal, HttpContext context, IEvidenceAttachmentService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); EvidenceAttachmentDownload download = await service.DownloadForRiderAsync(userId, id, ct); ApplyDownloadHeaders(context); return Results.File(download.Content, download.ContentType, download.FileName, enableRangeProcessing: false); });
        rider.MapPost("/{id}/delete", async (string id, ClaimsPrincipal principal, IEvidenceAttachmentService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); return Results.Ok(ApiResponse<EvidenceAttachmentResponse>.Ok(await service.DeleteForRiderAsync(userId, id, ct))); });

        RouteGroupBuilder monitor = endpoints.MapGroup("/api/v1/monitor/evidence-attachments").RequireAuthorization().WithTags("EvidenceAttachments");
        monitor.MapPost("", async (CreateEvidenceAttachmentRequest request, IValidator<CreateEvidenceAttachmentRequest> validator, ClaimsPrincipal principal, IEvidenceAttachmentService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); var validation = await validator.ValidateAsync(request, ct); if (!validation.IsValid) return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage))); return Results.Ok(ApiResponse<CreateEvidenceAttachmentResponse>.Ok(await service.CreateForMonitorAsync(userId, request, ct))); });
        monitor.MapPost("upload", async (HttpRequest request, ClaimsPrincipal principal, IEvidenceAttachmentService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); EvidenceUploadCommand command = await ParseUploadAsync(request, ct); return Results.Ok(ApiResponse<UploadEvidenceAttachmentResponse>.Ok(await service.UploadForMonitorAsync(userId, command, ct))); }).DisableAntiforgery();
        monitor.MapGet("/{id}/download", async (string id, ClaimsPrincipal principal, HttpContext context, IEvidenceAttachmentService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); EvidenceAttachmentDownload download = await service.DownloadForMonitorAsync(userId, id, ct); ApplyDownloadHeaders(context); return Results.File(download.Content, download.ContentType, download.FileName, enableRangeProcessing: false); });

        RouteGroupBuilder admin = endpoints.MapGroup("/api/v1/admin/evidence-attachments").RequireAuthorization().WithTags("EvidenceAttachments");
        admin.MapGet("", async (string? userId, string? incidentId, string? alertDispatchId, string? emergencyResolutionReportId, string? evidenceType, string? source, string? status, string? dateFrom, string? dateTo, int? pageNumber, int? pageSize, ClaimsPrincipal principal, EvidenceAttachmentQueryValidator validator, IEvidenceAttachmentService service, CancellationToken ct) => { string? adminUserId = GetUserId(principal); if (string.IsNullOrWhiteSpace(adminUserId)) return Results.Unauthorized(); EvidenceAttachmentQuery query = validator.Validate(userId, incidentId, alertDispatchId, emergencyResolutionReportId, evidenceType, source, status, ParseDate(dateFrom, nameof(dateFrom)), ParseDate(dateTo, nameof(dateTo)), pageNumber, pageSize); return Results.Ok(ApiResponse<GetEvidenceAttachmentsResponse>.Ok(await service.ListForAdminAsync(adminUserId, query, ct))); });
        admin.MapGet("/{id}", async (string id, ClaimsPrincipal principal, IEvidenceAttachmentService service, CancellationToken ct) => { string? adminUserId = GetUserId(principal); if (string.IsNullOrWhiteSpace(adminUserId)) return Results.Unauthorized(); return Results.Ok(ApiResponse<EvidenceAttachmentResponse>.Ok(await service.GetForAdminAsync(adminUserId, id, ct))); });
        admin.MapGet("/{id}/download", async (string id, ClaimsPrincipal principal, HttpContext context, IEvidenceAttachmentService service, CancellationToken ct) => { string? adminUserId = GetUserId(principal); if (string.IsNullOrWhiteSpace(adminUserId)) return Results.Unauthorized(); EvidenceAttachmentDownload download = await service.DownloadForAdminAsync(adminUserId, id, ct); ApplyDownloadHeaders(context); return Results.File(download.Content, download.ContentType, download.FileName, enableRangeProcessing: false); });
        return endpoints;
    }

    private static async Task<EvidenceUploadCommand> ParseUploadAsync(HttpRequest request, CancellationToken ct)
    {
        if (!request.HasFormContentType) throw new ValidationAppException("multipart/form-data is required.");
        IFormCollection form = await request.ReadFormAsync(ct);
        IFormFile? file = form.Files.GetFile("file");
        if (file is null) throw new ValidationAppException("file is required.");
        string? incidentId = form["incidentId"].FirstOrDefault();
        string? description = form["description"].FirstOrDefault();
        string? evidenceType = form["evidenceType"].FirstOrDefault() ?? form["type"].FirstOrDefault() ?? form["category"].FirstOrDefault();
        string? clientEvidenceId = form["clientEvidenceId"].FirstOrDefault();
        return new EvidenceUploadCommand(incidentId ?? string.Empty, file.OpenReadStream(), file.FileName, file.ContentType, file.Length, description, evidenceType, clientEvidenceId);
    }

    private static void ApplyDownloadHeaders(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store, no-cache";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";
    }

    private static DateTimeOffset? ParseDate(string? value, string name) { if (string.IsNullOrWhiteSpace(value)) return null; if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset parsed)) throw new ValidationAppException($"{name} must be a valid ISO UTC date."); return parsed.ToUniversalTime(); }
    private static string? GetUserId(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
}
