using System.Security.Claims;
using FluentValidation;
using MotoSOS.API.Common.Results;
using MotoSOS.API.Modules.EmergencyContacts.Application;
using MotoSOS.API.Modules.EmergencyContacts.Contracts;

namespace MotoSOS.API.Modules.EmergencyContacts.Endpoints;

public static class EmergencyContactEndpoints
{
    public static IEndpointRouteBuilder MapEmergencyContactEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/emergency-contacts")
            .RequireAuthorization()
            .WithTags("EmergencyContacts");

        group.MapGet(string.Empty, async (ClaimsPrincipal principal, IEmergencyContactService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            return Results.Ok(ApiResponse<GetEmergencyContactsResponse>.Ok(await service.GetMyContactsAsync(userId, cancellationToken)));
        });

        group.MapGet("/{id}", async (string id, ClaimsPrincipal principal, IEmergencyContactService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            return Results.Ok(ApiResponse<GetEmergencyContactResponse>.Ok(await service.GetMyContactAsync(userId, id, cancellationToken)));
        });

        group.MapPost(string.Empty, async (CreateEmergencyContactRequest request, IValidator<CreateEmergencyContactRequest> validator, ClaimsPrincipal principal, IEmergencyContactService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid) return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage)));
            CreateEmergencyContactResponse response = await service.CreateMyContactAsync(userId, request, cancellationToken);
            return Results.Created($"/api/v1/emergency-contacts/{response.Contact.Id}", ApiResponse<CreateEmergencyContactResponse>.Ok(response));
        });

        group.MapPut("/{id}", async (string id, UpdateEmergencyContactRequest request, IValidator<UpdateEmergencyContactRequest> validator, ClaimsPrincipal principal, IEmergencyContactService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid) return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage)));
            return Results.Ok(ApiResponse<UpdateEmergencyContactResponse>.Ok(await service.UpdateMyContactAsync(userId, id, request, cancellationToken)));
        });

        group.MapDelete("/{id}", async (string id, ClaimsPrincipal principal, IEmergencyContactService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            await service.DeleteMyContactAsync(userId, id, cancellationToken);
            return Results.NoContent();
        });

        group.MapPost("/{id}/invite", async (string id, ClaimsPrincipal principal, IEmergencyContactService service, CancellationToken cancellationToken) =>
        {
            string? userId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            return Results.Ok(ApiResponse<InviteEmergencyContactResponse>.Ok(await service.InviteMyContactAsync(userId, id, cancellationToken)));
        });

        group.MapGet("/invitations/{code}", async (string code, IEmergencyContactService service, CancellationToken cancellationToken) =>
        {
            return Results.Ok(ApiResponse<GetEmergencyContactInvitationResponse>.Ok(await service.GetInvitationAsync(code, cancellationToken)));
        });

        return endpoints;
    }

    private static string? GetUserId(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
}
