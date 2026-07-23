using FluentValidation;
using MotoSOS.API.Common.Results;
using MotoSOS.API.Modules.Auth.Application;
using MotoSOS.API.Modules.Auth.Contracts;

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

            LoginResponse response = await authService.LoginAsync(request, cancellationToken);
            return Results.Ok(ApiResponse<LoginResponse>.Ok(response));
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
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);

            if (!validation.IsValid)
            {
                return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage)));
            }

            var response = ApiResponse<object>.Fail(new ApiError("feature_not_implemented", "Access code login is prepared but pending an external provider."));
            return Results.Json(response, statusCode: StatusCodes.Status501NotImplemented);
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
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);

            if (!validation.IsValid)
            {
                return Results.BadRequest(ApiResponse<object>.Fail(new ApiError("validation_error", validation.Errors[0].ErrorMessage)));
            }

            await authService.LogoutAsync(request, cancellationToken);
            return Results.NoContent();
        });

        return endpoints;
    }
}
