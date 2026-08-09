using System.Globalization;
using System.Security.Claims;
using FluentValidation;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Common.Results;
using MotoSOS.API.Modules.Escalations.Application;
using MotoSOS.API.Modules.Escalations.Contracts;

namespace MotoSOS.API.Modules.Escalations.Endpoints;

public static class EmergencyEscalationEndpoints
{
    public static IEndpointRouteBuilder MapEmergencyEscalationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder rider = endpoints.MapGroup("/api/v1/rider/alert-dispatches").RequireAuthorization().WithTags("EmergencyEscalations");
        rider.MapPost("/{alertDispatchId}/escalate", async (string alertDispatchId, CreateEmergencyEscalationRequest request, IValidator<CreateEmergencyEscalationRequest> validator, ClaimsPrincipal principal, IEmergencyEscalationService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); var validation = await validator.ValidateAsync(request, ct); if (!validation.IsValid) return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage))); return Results.Ok(ApiResponse<CreateEmergencyEscalationResponse>.Ok(await service.EscalateAsync(userId, alertDispatchId, request, ct))); });
        rider.MapGet("/{alertDispatchId}/escalation-status", async (string alertDispatchId, ClaimsPrincipal principal, IEmergencyEscalationService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); return Results.Ok(ApiResponse<GetEmergencyEscalationResponse>.Ok(await service.GetForRiderAsync(userId, alertDispatchId, ct))); });
        rider.MapPost("/{alertDispatchId}/mark-unresolved", async (string alertDispatchId, ClaimsPrincipal principal, IEmergencyEscalationService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); return Results.Ok(ApiResponse<GetEmergencyEscalationResponse>.Ok(await service.MarkUnresolvedAsync(userId, alertDispatchId, ct))); });
        rider.MapPost("/{alertDispatchId}/cancel-escalation", async (string alertDispatchId, ClaimsPrincipal principal, IEmergencyEscalationService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); return Results.Ok(ApiResponse<GetEmergencyEscalationResponse>.Ok(await service.CancelAsync(userId, alertDispatchId, ct))); });

        endpoints.MapGet("/api/v1/monitor/alerts/{notificationDeliveryAttemptId}/escalation-status", async (string notificationDeliveryAttemptId, ClaimsPrincipal principal, IEmergencyEscalationService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); return Results.Ok(ApiResponse<GetEmergencyEscalationResponse>.Ok(await service.GetForMonitorAsync(userId, notificationDeliveryAttemptId, ct))); }).RequireAuthorization().WithTags("EmergencyEscalations");

        endpoints.MapGet("/api/v1/admin/escalations", async (string? status, string? reason, string? level, string? dateFrom, string? dateTo, int? pageNumber, int? pageSize, ClaimsPrincipal principal, EscalationQueryValidator validator, IEmergencyEscalationService service, CancellationToken ct) => { string? userId = GetUserId(principal); if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized(); EmergencyEscalationQuery query = validator.Validate(status, reason, level, ParseDate(dateFrom, nameof(dateFrom)), ParseDate(dateTo, nameof(dateTo)), pageNumber, pageSize); return Results.Ok(ApiResponse<GetEmergencyEscalationsResponse>.Ok(await service.ListForAdminAsync(userId, query, ct))); }).RequireAuthorization().WithTags("EmergencyEscalations");
        return endpoints;
    }

    private static DateTimeOffset? ParseDate(string? value, string name) { if (string.IsNullOrWhiteSpace(value)) return null; if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset parsed)) throw new ValidationAppException($"{name} must be a valid ISO UTC date."); return parsed.ToUniversalTime(); }
    private static string? GetUserId(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
}
