using MotoSOS.API.Modules.Auth.Contracts;

namespace MotoSOS.API.Modules.Auth.Application;

public interface IAuthService
{
    Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<RefreshTokenResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken);

    Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken);
}
