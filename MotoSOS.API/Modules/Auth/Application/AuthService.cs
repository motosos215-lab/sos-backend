using Microsoft.Extensions.Options;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.Auth.Contracts;
using MotoSOS.API.Modules.Auth.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;
using MotoSOS.API.Security.Hashing;
using MotoSOS.API.Security.Tokens;

namespace MotoSOS.API.Modules.Auth.Application;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenGenerator _refreshTokenGenerator;
    private readonly IClock _clock;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IRefreshTokenGenerator refreshTokenGenerator,
        IClock clock,
        IOptions<JwtOptions> jwtOptions,
        ILogger<AuthService> logger)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _refreshTokenGenerator = refreshTokenGenerator;
        _clock = clock;
        _jwtOptions = jwtOptions.Value;
        _logger = logger;
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        string normalizedEmail = NormalizeEmail(request.Email);
        if (!request.AcceptTerms)
        {
            throw new TermsNotAcceptedAppException();
        }

        User? existingUser = await _users.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (existingUser is not null)
        {
            throw new UserAlreadyExistsAppException();
        }

        DateTimeOffset now = _clock.UtcNow;

        var user = new User
        {
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.Hash(request.Password),
            FullName = request.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            Role = MapPublicAccountType(request.AccountType),
            IsActive = true,
            CreatedAtUtc = now,
            AcceptedTermsAtUtc = now
        };

        await _users.AddAsync(user, cancellationToken);

        return new RegisterResponse(ToAuthUser(user));
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        User user = await GetActiveUserForLoginAsync(request.Email, request.Password, cancellationToken);
        user.LastLoginAtUtc = _clock.UtcNow;
        user.UpdatedAtUtc = _clock.UtcNow;

        await _users.UpdateAsync(user, cancellationToken);

        TokenResult accessToken = _jwtTokenService.CreateAccessToken(user);
        string plainRefreshValue = _refreshTokenGenerator.CreateToken();
        string refreshHash = _refreshTokenGenerator.HashToken(plainRefreshValue);
        int refreshTokenDays = request.RememberMe ? _jwtOptions.RefreshTokenRememberMeDays : _jwtOptions.RefreshTokenDays;

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            CreatedAtUtc = _clock.UtcNow,
            ExpiresAtUtc = _clock.UtcNow.AddDays(refreshTokenDays)
        };

        await _refreshTokens.AddAsync(refreshToken, cancellationToken);

        return new LoginResponse(accessToken.AccessToken, plainRefreshValue, accessToken.ExpiresAtUtc, ToAuthUser(user));
    }

    public async Task RequestPasswordResetAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        _ = await _users.GetByEmailAsync(NormalizeEmail(request.Email), cancellationToken);
        _logger.LogInformation("Password recovery was requested. User existence is intentionally not disclosed.");
    }

    public async Task RequestAccessCodeAsync(RequestAccessCodeRequest request, CancellationToken cancellationToken)
    {
        _ = await _users.GetByEmailAsync(NormalizeEmail(request.Email), cancellationToken);
        _logger.LogInformation("Access code was requested. User existence is intentionally not disclosed.");
    }

    public async Task<RefreshTokenResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        string incomingHash = _refreshTokenGenerator.HashToken(request.RefreshToken);
        RefreshToken? storedRefreshToken = await _refreshTokens.GetByHashAsync(incomingHash, cancellationToken);

        if (storedRefreshToken is null || !storedRefreshToken.IsActive)
        {
            throw new UnauthorizedAppException("Invalid authentication credentials.");
        }

        User? user = await _users.GetByIdAsync(storedRefreshToken.UserId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            throw new InvalidCredentialsAppException();
        }

        string plainRefreshValue = _refreshTokenGenerator.CreateToken();
        string newRefreshHash = _refreshTokenGenerator.HashToken(plainRefreshValue);

        storedRefreshToken.RevokedAtUtc = _clock.UtcNow;
        storedRefreshToken.ReplacedByTokenHash = newRefreshHash;
        await _refreshTokens.UpdateAsync(storedRefreshToken, cancellationToken);

        var replacement = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = newRefreshHash,
            CreatedAtUtc = _clock.UtcNow,
            ExpiresAtUtc = _clock.UtcNow.AddDays(_jwtOptions.RefreshTokenDays)
        };

        await _refreshTokens.AddAsync(replacement, cancellationToken);

        TokenResult accessToken = _jwtTokenService.CreateAccessToken(user);

        return new RefreshTokenResponse(accessToken.AccessToken, plainRefreshValue, accessToken.ExpiresAtUtc);
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        string incomingHash = _refreshTokenGenerator.HashToken(request.RefreshToken);
        RefreshToken? storedRefreshToken = await _refreshTokens.GetByHashAsync(incomingHash, cancellationToken);

        if (storedRefreshToken is null || storedRefreshToken.RevokedAtUtc is not null)
        {
            return;
        }

        storedRefreshToken.RevokedAtUtc = _clock.UtcNow;
        await _refreshTokens.UpdateAsync(storedRefreshToken, cancellationToken);
    }

    private async Task<User> GetActiveUserForLoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByEmailAsync(NormalizeEmail(email), cancellationToken);

        if (user is null || !user.IsActive || !_passwordHasher.Verify(password, user.PasswordHash))
        {
            throw new InvalidCredentialsAppException();
        }

        return user;
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static UserRole MapPublicAccountType(string accountType)
    {
        string normalized = accountType.Trim();

        if (string.Equals(normalized, "Monitor", StringComparison.OrdinalIgnoreCase))
        {
            return UserRole.Monitor;
        }

        return UserRole.Rider;
    }

    private static AuthUserResponse ToAuthUser(User user)
    {
        return new AuthUserResponse(user.Id, user.Email, user.FullName, user.PhoneNumber, user.Role.ToString(), user.IsActive);
    }
}
