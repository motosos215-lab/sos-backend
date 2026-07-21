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
