using System.Security.Claims;
using FluentValidation;
using MotoSOS.API.Common.Results;
using MotoSOS.API.Modules.Auth.Application;
using MotoSOS.API.Modules.Auth.Contracts;
using MotoSOS.API.Modules.Auth.Sessions.Application;
using MotoSOS.API.Modules.Auth.Sessions.Contracts;

namespace MotoSOS.API.Modules.Auth.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/auth")
            .RequireRateLimiting("AuthRateLimit")
            .WithTags("Auth");

        group.MapPost("/register", async (
            RegisterRequest request,
            IValidator<RegisterRequest> validator,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);

            if (!validation.IsValid)
            {
                return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage)));
            }

            if (!request.AcceptTerms)
            {
                return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("terms_not_accepted", "Terms and conditions must be accepted.")));
            }

            RegisterResponse response = await authService.RegisterAsync(request, cancellationToken);
            return Results.Created($"/api/v1/users/{response.User.Id}", ApiResponse<RegisterResponse>.Ok(response));
        });

        group.MapPost("/login", async (
            LoginRequest request,
            IValidator<LoginRequest> validator,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);

            if (!validation.IsValid)
            {
                return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage)));
            }

            try
            {
                LoginResponse response = await authService.LoginAsync(request, cancellationToken);
                return Results.Ok(ApiResponse<LoginResponse>.Ok(response));
            }
            catch (ActiveSessionExistsAppException exception)
            {
                return Results.Conflict(new ApiResponse<ActiveSessionConflictResponse>(false, exception.DataPayload, new ApiError(exception.Code, exception.Message)));
            }
        });

        group.MapPost("/forgot-password", async (
            ForgotPasswordRequest request,
            IValidator<ForgotPasswordRequest> validator,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);

            if (!validation.IsValid)
            {
                return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage)));
            }

            await authService.RequestPasswordResetAsync(request, cancellationToken);
            return Results.NoContent();
        });

        group.MapPost("/reset-password", async (
            ResetPasswordRequest request,
            IValidator<ResetPasswordRequest> validator,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);

            if (!validation.IsValid)
            {
                return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage)));
            }

            await authService.ResetPasswordAsync(request, cancellationToken);
            return Results.Ok(new ApiResponse<object>(true));
        });

        group.MapPost("/request-access-code", async (
            RequestAccessCodeRequest request,
            IValidator<RequestAccessCodeRequest> validator,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);

            if (!validation.IsValid)
            {
                return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage)));
            }

            await authService.RequestAccessCodeAsync(request, cancellationToken);
            return Results.NoContent();
        });

        group.MapPost("/login-with-code", async (
            LoginWithCodeRequest request,
            IValidator<LoginWithCodeRequest> validator,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);

            if (!validation.IsValid)
            {
                return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage)));
            }

            try
            {
                LoginResponse response = await authService.LoginWithCodeAsync(request, cancellationToken);
                return Results.Ok(ApiResponse<LoginResponse>.Ok(response));
            }
            catch (ActiveSessionExistsAppException exception)
            {
                return Results.Conflict(new ApiResponse<ActiveSessionConflictResponse>(false, exception.DataPayload, new ApiError(exception.Code, exception.Message)));
            }
        });

        group.MapPost("/sessions/takeover", async (
            TakeoverSessionRequest request,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                TakeoverSessionResponse response = await authService.TakeoverAsync(request, cancellationToken);
                return Results.Ok(ApiResponse<TakeoverSessionResponse>.Ok(response));
            }
            catch (ActiveTripTransferRequiredAppException exception)
            {
                return Results.Conflict(new ApiResponse<object>(false, new { activeTrip = exception.DataPayload }, new ApiError(exception.Code, exception.Message)));
            }
            catch (ActiveSessionExistsAppException exception)
            {
                return Results.Conflict(new ApiResponse<ActiveSessionConflictResponse>(false, exception.DataPayload, new ApiError(exception.Code, exception.Message)));
            }
        });

        group.MapPost("/refresh", async (
            RefreshTokenRequest request,
            IValidator<RefreshTokenRequest> validator,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);

            if (!validation.IsValid)
            {
                return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage)));
            }

            RefreshTokenResponse response = await authService.RefreshAsync(request, cancellationToken);
            return Results.Ok(ApiResponse<RefreshTokenResponse>.Ok(response));
        });

        group.MapPost("/logout", async (
            LogoutRequest request,
            IValidator<LogoutRequest> validator,
            IAuthService authService,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);

            if (!validation.IsValid)
            {
                return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage)));
            }

            await authService.LogoutAsync(GetUserId(principal), GetSessionId(principal), request, cancellationToken);
            return Results.Ok(ApiResponse<object>.Ok(new { loggedOut = true }));
        }).RequireAuthorization();

        return endpoints;
    }

    private static string? GetUserId(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
    private static string? GetSessionId(ClaimsPrincipal principal) => principal.FindFirstValue("sid");
}
