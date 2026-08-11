using Microsoft.Extensions.Options;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AuditLogs.Application;
using MotoSOS.API.Modules.AuditLogs.Domain;
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
    private readonly IAuthCodeRepository _authCodes;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuthCodeHasher _authCodeHasher;
    private readonly IAuthCodeGenerator _authCodeGenerator;
    private readonly IAuthCodeDeliveryProvider _authCodeDeliveryProvider;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenGenerator _refreshTokenGenerator;
    private readonly IClock _clock;
    private readonly JwtOptions _jwtOptions;
    private readonly AuthCodeOptions _authCodeOptions;
    private readonly ILogger<AuthService> _logger;
    private readonly IAuditLogService? _auditLogs;

    public AuthService(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IAuthCodeRepository authCodes,
        IPasswordHasher passwordHasher,
        IAuthCodeHasher authCodeHasher,
        IAuthCodeGenerator authCodeGenerator,
        IAuthCodeDeliveryProvider authCodeDeliveryProvider,
        IJwtTokenService jwtTokenService,
        IRefreshTokenGenerator refreshTokenGenerator,
        IClock clock,
        IOptions<JwtOptions> jwtOptions,
        IOptions<AuthCodeOptions> authCodeOptions,
        ILogger<AuthService> logger,
        IAuditLogService? auditLogs = null)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _authCodes = authCodes;
        _passwordHasher = passwordHasher;
        _authCodeHasher = authCodeHasher;
        _authCodeGenerator = authCodeGenerator;
        _authCodeDeliveryProvider = authCodeDeliveryProvider;
        _jwtTokenService = jwtTokenService;
        _refreshTokenGenerator = refreshTokenGenerator;
        _clock = clock;
        _jwtOptions = jwtOptions.Value;
        _authCodeOptions = authCodeOptions.Value;
        _logger = logger;
        _auditLogs = auditLogs;
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
        return await CreateLoginResponseAsync(user, request.RememberMe, cancellationToken);
    }

    public async Task RequestPasswordResetAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await RequestCodeAsync(request.Email, AuthCodePurpose.PasswordReset, cancellationToken);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        EnsureAuthCodesEnabled();
        (AuthCode authCode, User user) = await ValidateCodeAsync(request.Email, request.Code, AuthCodePurpose.PasswordReset, cancellationToken);

        DateTimeOffset now = _clock.UtcNow;
        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.UpdatedAtUtc = now;
        await _users.UpdateAsync(user, cancellationToken);
        await _refreshTokens.RevokeActiveByUserIdAsync(user.Id, now, cancellationToken);

        authCode.Status = AuthCodeStatus.Used;
        authCode.UsedAtUtc = now;
        authCode.LastAttemptAtUtc = now;
        await _authCodes.UpdateAsync(authCode, cancellationToken);
    }

    public async Task RequestAccessCodeAsync(RequestAccessCodeRequest request, CancellationToken cancellationToken)
    {
        await RequestCodeAsync(request.Email, AuthCodePurpose.AccessLogin, cancellationToken);
    }

    public async Task<LoginResponse> LoginWithCodeAsync(LoginWithCodeRequest request, CancellationToken cancellationToken)
    {
        EnsureAuthCodesEnabled();
        (AuthCode authCode, User user) = await ValidateCodeAsync(request.Email, request.Code, AuthCodePurpose.AccessLogin, cancellationToken);

        DateTimeOffset now = _clock.UtcNow;
        authCode.Status = AuthCodeStatus.Used;
        authCode.UsedAtUtc = now;
        authCode.LastAttemptAtUtc = now;
        await _authCodes.UpdateAsync(authCode, cancellationToken);

        return await CreateLoginResponseAsync(user, rememberMe: false, cancellationToken);
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
        User? user = await _users.GetByIdAsync(storedRefreshToken.UserId, cancellationToken);
        if (user is not null) await (_auditLogs?.RecordAsync(user.Id, user.Role.ToString(), AuditAction.AuthLogout, AuditModule.Auth, "User", user.Id, AuditOutcome.Success, null, null, null, null, cancellationToken) ?? Task.CompletedTask);
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

    private async Task RequestCodeAsync(string email, AuthCodePurpose purpose, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Auth code was requested. User existence is intentionally not disclosed.");

        if (!_authCodeOptions.Enabled)
        {
            return;
        }

        string normalizedEmail = NormalizeEmail(email);
        DateTimeOffset now = _clock.UtcNow;
        AuthCode? latestCode = await _authCodes.GetLatestByEmailAndPurposeAsync(normalizedEmail, purpose, cancellationToken);
        if (latestCode is not null && latestCode.CreatedAtUtc.AddMinutes(_authCodeOptions.RateLimitMinutes) > now)
        {
            return;
        }

        User? user = await _users.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return;
        }

        await _authCodes.RevokeActiveAsync(normalizedEmail, purpose, now, cancellationToken);

        string code = _authCodeGenerator.CreateCode(_authCodeOptions.CodeLength);
        var authCode = new AuthCode
        {
            EmailNormalized = normalizedEmail,
            UserId = user.Id,
            Purpose = purpose,
            CodeHash = _authCodeHasher.Hash(code),
            Status = AuthCodeStatus.Active,
            Attempts = 0,
            MaxAttempts = Math.Max(1, _authCodeOptions.MaxAttempts),
            ExpiresAtUtc = now.AddMinutes(Math.Max(1, _authCodeOptions.TtlMinutes)),
            CreatedAtUtc = now,
            DeliveryChannel = _authCodeDeliveryProvider.Channel,
            DeliveryStatus = AuthCodeDeliveryStatus.Pending
        };

        authCode.DeliveryStatus = await _authCodeDeliveryProvider.DeliverAsync(normalizedEmail, purpose, code, cancellationToken);
        await _authCodes.AddAsync(authCode, cancellationToken);
    }

    private async Task<(AuthCode AuthCode, User User)> ValidateCodeAsync(string email, string code, AuthCodePurpose purpose, CancellationToken cancellationToken)
    {
        string normalizedEmail = NormalizeEmail(email);
        DateTimeOffset now = _clock.UtcNow;
        AuthCode? authCode = await _authCodes.GetLatestByEmailAndPurposeAsync(normalizedEmail, purpose, cancellationToken);
        if (authCode is null || authCode.Status != AuthCodeStatus.Active)
        {
            throw new AuthCodeInvalidAppException();
        }

        if (authCode.ExpiresAtUtc <= now)
        {
            authCode.Status = AuthCodeStatus.Expired;
            await _authCodes.UpdateAsync(authCode, cancellationToken);
            throw new AuthCodeInvalidAppException();
        }

        if (authCode.Attempts >= authCode.MaxAttempts)
        {
            authCode.Status = AuthCodeStatus.Failed;
            await _authCodes.UpdateAsync(authCode, cancellationToken);
            throw new AuthCodeInvalidAppException();
        }

        if (!_authCodeHasher.Verify(code, authCode.CodeHash))
        {
            authCode.Attempts++;
            authCode.LastAttemptAtUtc = now;
            if (authCode.Attempts >= authCode.MaxAttempts)
            {
                authCode.Status = AuthCodeStatus.Failed;
            }

            await _authCodes.UpdateAsync(authCode, cancellationToken);
            throw new AuthCodeInvalidAppException();
        }

        if (authCode.UserId is null)
        {
            throw new AuthCodeInvalidAppException();
        }

        User? user = await _users.GetByIdAsync(authCode.UserId, cancellationToken);
        if (user is null || !user.IsActive || !string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new AuthCodeInvalidAppException();
        }

        return (authCode, user);
    }

    private async Task<LoginResponse> CreateLoginResponseAsync(User user, bool rememberMe, CancellationToken cancellationToken)
    {
        DateTimeOffset now = _clock.UtcNow;
        user.LastLoginAtUtc = now;
        user.UpdatedAtUtc = now;

        await _users.UpdateAsync(user, cancellationToken);

        TokenResult accessToken = _jwtTokenService.CreateAccessToken(user);
        string plainRefreshValue = _refreshTokenGenerator.CreateToken();
        string refreshHash = _refreshTokenGenerator.HashToken(plainRefreshValue);
        int refreshTokenDays = rememberMe ? _jwtOptions.RefreshTokenRememberMeDays : _jwtOptions.RefreshTokenDays;

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(refreshTokenDays)
        };

        await _refreshTokens.AddAsync(refreshToken, cancellationToken);

        await (_auditLogs?.RecordAsync(user.Id, user.Role.ToString(), AuditAction.AuthLogin, AuditModule.Auth, "User", user.Id, AuditOutcome.Success, null, null, null, new Dictionary<string, string> { ["rememberMe"] = rememberMe.ToString() }, cancellationToken) ?? Task.CompletedTask);

        return new LoginResponse(accessToken.AccessToken, plainRefreshValue, accessToken.ExpiresAtUtc, ToAuthUser(user));
    }

    private void EnsureAuthCodesEnabled()
    {
        if (!_authCodeOptions.Enabled)
        {
            throw new FeatureDisabledAppException("Authentication codes are disabled.");
        }
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
