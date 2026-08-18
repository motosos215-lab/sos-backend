using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AuditLogs.Application;
using MotoSOS.API.Modules.AuditLogs.Domain;
using MotoSOS.API.Modules.Auth.Contracts;
using MotoSOS.API.Modules.Auth.Domain;
using MotoSOS.API.Modules.Auth.Sessions.Application;
using MotoSOS.API.Modules.Auth.Sessions.Contracts;
using MotoSOS.API.Modules.Auth.Sessions.Domain;
using MotoSOS.API.Modules.Devices.Application;
using MotoSOS.API.Modules.Devices.Domain;
using MotoSOS.API.Modules.PushNotificationTokens.Application;
using MotoSOS.API.Modules.Trips.Application;
using MotoSOS.API.Modules.Trips.Domain;
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
    private readonly IUserSessionRepository _sessions;
    private readonly ISessionTakeoverTokenRepository _takeoverTokens;
    private readonly ITripRepository _trips;
    private readonly IUserDeviceRepository _devices;
    private readonly IPushNotificationTokenRepository _pushTokens;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuthCodeHasher _authCodeHasher;
    private readonly IAuthCodeGenerator _authCodeGenerator;
    private readonly IAuthCodeDeliveryProvider _authCodeDeliveryProvider;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenGenerator _refreshTokenGenerator;
    private readonly IClock _clock;
    private readonly JwtOptions _jwtOptions;
    private readonly AuthCodeOptions _authCodeOptions;
    private readonly UserSessionOptions _sessionOptions;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<AuthService> _logger;
    private readonly IAuditLogService? _auditLogs;

    public AuthService(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IAuthCodeRepository authCodes,
        IUserSessionRepository sessions,
        ISessionTakeoverTokenRepository takeoverTokens,
        ITripRepository trips,
        IUserDeviceRepository devices,
        IPushNotificationTokenRepository pushTokens,
        IPasswordHasher passwordHasher,
        IAuthCodeHasher authCodeHasher,
        IAuthCodeGenerator authCodeGenerator,
        IAuthCodeDeliveryProvider authCodeDeliveryProvider,
        IJwtTokenService jwtTokenService,
        IRefreshTokenGenerator refreshTokenGenerator,
        IClock clock,
        IOptions<JwtOptions> jwtOptions,
        IOptions<AuthCodeOptions> authCodeOptions,
        IOptions<UserSessionOptions> sessionOptions,
        IHostEnvironment environment,
        ILogger<AuthService> logger,
        IAuditLogService? auditLogs = null)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _authCodes = authCodes;
        _sessions = sessions;
        _takeoverTokens = takeoverTokens;
        _trips = trips;
        _devices = devices;
        _pushTokens = pushTokens;
        _passwordHasher = passwordHasher;
        _authCodeHasher = authCodeHasher;
        _authCodeGenerator = authCodeGenerator;
        _authCodeDeliveryProvider = authCodeDeliveryProvider;
        _jwtTokenService = jwtTokenService;
        _refreshTokenGenerator = refreshTokenGenerator;
        _clock = clock;
        _jwtOptions = jwtOptions.Value;
        _authCodeOptions = authCodeOptions.Value;
        _sessionOptions = sessionOptions.Value;
        _environment = environment;
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
        return await CreateLoginResponseAsync(user, request.RememberMe, request.ClientDevice, request.ClientType, cancellationToken);
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

        LoginResponse response = await CreateLoginResponseAsync(user, rememberMe: false, request.ClientDevice, request.ClientType, cancellationToken);
        DateTimeOffset now = _clock.UtcNow;
        authCode.Status = AuthCodeStatus.Used;
        authCode.UsedAtUtc = now;
        authCode.LastAttemptAtUtc = now;
        await _authCodes.UpdateAsync(authCode, cancellationToken);
        return response;
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

        UserSession? session = null;
        if (string.IsNullOrWhiteSpace(storedRefreshToken.SessionId))
        {
            throw new SessionRevokedAppException();
        }

        session = await _sessions.GetByIdAsync(storedRefreshToken.SessionId, cancellationToken);
        if (session is null || session.UserId != user.Id || session.RevokedAtUtc is not null)
        {
            throw new SessionRevokedAppException();
        }

        await TouchSessionAsync(session, cancellationToken);

        string plainRefreshValue = _refreshTokenGenerator.CreateToken();
        string newRefreshHash = _refreshTokenGenerator.HashToken(plainRefreshValue);

        storedRefreshToken.RevokedAtUtc = _clock.UtcNow;
        storedRefreshToken.ReplacedByTokenHash = newRefreshHash;
        await _refreshTokens.UpdateAsync(storedRefreshToken, cancellationToken);

        var replacement = new RefreshToken
        {
            UserId = user.Id,
            SessionId = storedRefreshToken.SessionId,
            TokenHash = newRefreshHash,
            CreatedAtUtc = _clock.UtcNow,
            ExpiresAtUtc = _clock.UtcNow.AddDays(_jwtOptions.RefreshTokenDays)
        };

        await _refreshTokens.AddAsync(replacement, cancellationToken);

        TokenResult accessToken = _jwtTokenService.CreateAccessToken(user, session?.Id);

        return new RefreshTokenResponse(accessToken.AccessToken, plainRefreshValue, accessToken.ExpiresAtUtc, session is null ? null : ToSessionResponse(session));
    }

    public async Task LogoutAsync(string? userId, string? sessionId, LogoutRequest request, CancellationToken cancellationToken)
    {
        DateTimeOffset now = _clock.UtcNow;
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            UserSession? session = await _sessions.GetByIdAsync(sessionId, cancellationToken);
            if (session is not null && session.RevokedAtUtc is null)
            {
                session.RevokedAtUtc = now;
                session.RevokedReason = "logout";
                session.UpdatedAtUtc = now;
                await _sessions.UpdateAsync(session, cancellationToken);
                await _refreshTokens.RevokeActiveBySessionIdAsync(session.Id, now, cancellationToken);
                await _pushTokens.RevokeActiveTokensBySessionIdAsync(session.Id, now, cancellationToken);
            }
        }

        string incomingHash = _refreshTokenGenerator.HashToken(request.RefreshToken);
        RefreshToken? storedRefreshToken = await _refreshTokens.GetByHashAsync(incomingHash, cancellationToken);

        if (storedRefreshToken is null || storedRefreshToken.RevokedAtUtc is not null)
        {
            return;
        }

        storedRefreshToken.RevokedAtUtc = now;
        await _refreshTokens.UpdateAsync(storedRefreshToken, cancellationToken);
        User? user = await _users.GetByIdAsync(storedRefreshToken.UserId, cancellationToken);
        if (user is not null) await (_auditLogs?.RecordAsync(user.Id, user.Role.ToString(), AuditAction.AuthLogout, AuditModule.Auth, "User", user.Id, AuditOutcome.Success, null, null, null, null, cancellationToken) ?? Task.CompletedTask);
    }

    public async Task<TakeoverSessionResponse> TakeoverAsync(TakeoverSessionRequest request, CancellationToken cancellationToken)
    {
        string tokenHash = _refreshTokenGenerator.HashToken(request.TakeoverToken);
        SessionTakeoverToken? token = await _takeoverTokens.GetByHashAsync(tokenHash, cancellationToken);
        DateTimeOffset now = _clock.UtcNow;
        if (token is null || token.RevokedAtUtc is not null) throw new TakeoverTokenInvalidAppException();
        if (token.UsedAtUtc is not null) throw new TakeoverTokenInvalidAppException("takeover_token_already_used");
        if (token.ExpiresAtUtc <= now) throw new TakeoverTokenInvalidAppException("takeover_token_expired");

        ClientDeviceRequest? clientDevice = NormalizeClientDevice(request.ClientDevice, token.SessionType);
        if (token.SessionType == UserSessionType.MobileApp && clientDevice is null) throw new TakeoverTokenInvalidAppException();
        if (clientDevice is not null && !string.Equals(token.NewClientDeviceId, clientDevice.ClientDeviceId, StringComparison.OrdinalIgnoreCase)) throw new TakeoverTokenInvalidAppException();

        User user = await _users.GetByIdAsync(token.UserId, cancellationToken) ?? throw new TakeoverTokenInvalidAppException();
        if (!user.IsActive || user.Role.ToString() != token.Role) throw new TakeoverTokenInvalidAppException();
        UserSession oldSession = await _sessions.GetByIdAsync(token.ActiveSessionId, cancellationToken) ?? throw new TakeoverTokenInvalidAppException();
        if (oldSession.UserId != user.Id || oldSession.SessionType != token.SessionType || oldSession.RevokedAtUtc is not null) throw new TakeoverTokenInvalidAppException();

        ActiveTripSessionResponse? activeTrip = null;
        Trip? tripToTransfer = null;
        UserDevice? deviceToTransfer = null;
        if (token.HasActiveTrip)
        {
            Trip trip = await _trips.GetByIdAsync(token.ActiveTripId ?? string.Empty, cancellationToken) ?? throw new AppException("Active trip is not available.", StatusCodes.Status409Conflict, "active_trip_not_available");
            if (trip.UserId != user.Id || trip.Status != TripStatus.Active) throw new AppException("Active trip is not available.", StatusCodes.Status409Conflict, "active_trip_not_available");
            if (!request.TransferActiveTrip)
            {
                throw new ActiveTripTransferRequiredAppException(ToActiveTripResponse(trip));
            }

            UserDevice device = await _devices.GetByIdAsync(request.MobileDeviceId?.Trim() ?? string.Empty, cancellationToken) ?? throw new AppException("Device is not available.", StatusCodes.Status409Conflict, "device_not_available");
            if (device.UserId != user.Id || !device.IsActive || device.RevokedAtUtc is not null || device.LinkStatus != DeviceLinkStatus.Linked || device.DeviceType != DeviceType.MobileApp)
            {
                throw new AppException("Device is not available.", StatusCodes.Status409Conflict, "device_not_available");
            }

            tripToTransfer = trip;
            deviceToTransfer = device;
        }

        bool used = await _takeoverTokens.MarkUsedAsync(token.Id, now, cancellationToken);
        if (!used) throw new TakeoverTokenInvalidAppException("takeover_token_already_used");

        if (tripToTransfer is not null && deviceToTransfer is not null)
        {
            bool transferred = await _trips.TransferActiveMobileDeviceAsync(tripToTransfer.Id, user.Id, deviceToTransfer.Id, now, cancellationToken);
            if (!transferred) throw new AppException("Active trip is not available.", StatusCodes.Status409Conflict, "active_trip_not_available");
            tripToTransfer.MobileDeviceId = deviceToTransfer.Id;
            tripToTransfer.UpdatedAtUtc = now;
            activeTrip = ToActiveTripResponse(tripToTransfer, transferred: true);
        }

        oldSession.RevokedAtUtc = now;
        oldSession.RevokedReason = "takeover";
        oldSession.UpdatedAtUtc = now;
        await _sessions.UpdateAsync(oldSession, cancellationToken);
        await _refreshTokens.RevokeActiveBySessionIdAsync(oldSession.Id, now, cancellationToken);
        await _pushTokens.RevokeActiveTokensBySessionIdAsync(oldSession.Id, now, cancellationToken);

        UserSession newSession = CreateSession(user, token.SessionType, clientDevice, now, "takeover");
        (UserSession savedSession, bool created) = await _sessions.AddActiveOrGetExistingAsync(newSession, cancellationToken);
        if (!created && savedSession.ClientDeviceId != newSession.ClientDeviceId) throw new ActiveSessionExistsAppException(await CreateActiveSessionConflictAsync(user, savedSession, newSession.SessionType, clientDevice, cancellationToken));
        oldSession.ReplacedBySessionId = savedSession.Id;
        await _sessions.UpdateAsync(oldSession, cancellationToken);

        LoginResponse login = await IssueTokensAsync(user, savedSession, rememberMe: false, cancellationToken);
        return new TakeoverSessionResponse(login.AccessToken, login.RefreshToken, login.AccessTokenExpiresAtUtc, login.Session!, activeTrip, login.User);
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

        await _authCodes.AddAsync(authCode, cancellationToken);

        authCode.DeliveryStatus = await _authCodeDeliveryProvider.DeliverAsync(normalizedEmail, purpose, code, cancellationToken);
        await _authCodes.UpdateAsync(authCode, cancellationToken);
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

    private async Task<LoginResponse> CreateLoginResponseAsync(User user, bool rememberMe, ClientDeviceRequest? clientDeviceRequest, string? clientType, CancellationToken cancellationToken)
    {
        DateTimeOffset now = _clock.UtcNow;
        user.LastLoginAtUtc = now;
        user.UpdatedAtUtc = now;

        await _users.UpdateAsync(user, cancellationToken);

        UserSessionType sessionType = DetermineSessionType(user, clientDeviceRequest, clientType);
        ClientDeviceRequest? clientDevice = NormalizeClientDevice(clientDeviceRequest, sessionType) ?? CreateTestingClientDevice(user, sessionType);
        if (sessionType == UserSessionType.MobileApp && clientDevice is null)
        {
            throw new ValidationAppException("clientDevice is required for MobileApp login.");
        }

        UserSession candidate = CreateSession(user, sessionType, clientDevice, now, "login");
        (UserSession session, bool created) = await _sessions.AddActiveOrGetExistingAsync(candidate, cancellationToken);
        if (!created)
        {
            if (sessionType == UserSessionType.MobileApp && !string.Equals(session.ClientDeviceId, candidate.ClientDeviceId, StringComparison.OrdinalIgnoreCase))
            {
                throw new ActiveSessionExistsAppException(await CreateActiveSessionConflictAsync(user, session, sessionType, clientDevice, cancellationToken));
            }

            await TouchSessionAsync(session, cancellationToken);
        }

        await (_auditLogs?.RecordAsync(user.Id, user.Role.ToString(), AuditAction.AuthLogin, AuditModule.Auth, "User", user.Id, AuditOutcome.Success, null, null, null, new Dictionary<string, string> { ["rememberMe"] = rememberMe.ToString() }, cancellationToken) ?? Task.CompletedTask);
        return await IssueTokensAsync(user, session, rememberMe, cancellationToken);
    }

    private async Task<LoginResponse> IssueTokensAsync(User user, UserSession? session, bool rememberMe, CancellationToken cancellationToken)
    {
        DateTimeOffset now = _clock.UtcNow;
        TokenResult accessToken = _jwtTokenService.CreateAccessToken(user, session?.Id);
        string plainRefreshValue = _refreshTokenGenerator.CreateToken();
        string refreshHash = _refreshTokenGenerator.HashToken(plainRefreshValue);
        int refreshTokenDays = rememberMe ? _jwtOptions.RefreshTokenRememberMeDays : _jwtOptions.RefreshTokenDays;

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            SessionId = session?.Id,
            TokenHash = refreshHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(refreshTokenDays)
        };

        await _refreshTokens.AddAsync(refreshToken, cancellationToken);

        return new LoginResponse(accessToken.AccessToken, plainRefreshValue, accessToken.ExpiresAtUtc, ToAuthUser(user), session is null ? null : ToSessionResponse(session));
    }

    private async Task<ActiveSessionConflictResponse> CreateActiveSessionConflictAsync(User user, UserSession activeSession, UserSessionType sessionType, ClientDeviceRequest? newClientDevice, CancellationToken cancellationToken)
    {
        DateTimeOffset now = _clock.UtcNow;
        Trip? trip = user.Role == UserRole.Rider ? await _trips.GetActiveByUserIdAsync(user.Id, cancellationToken) : null;
        string plainToken = _refreshTokenGenerator.CreateToken();
        var takeover = new SessionTakeoverToken
        {
            UserId = user.Id,
            Role = user.Role.ToString(),
            SessionType = sessionType,
            NewClientDeviceId = newClientDevice?.ClientDeviceId ?? string.Empty,
            NewDeviceName = newClientDevice?.DeviceName ?? DefaultDeviceName(sessionType),
            NewPlatform = newClientDevice?.Platform ?? DefaultPlatform(sessionType),
            NewOsVersion = newClientDevice?.OsVersion,
            NewAppVersion = newClientDevice?.AppVersion,
            TokenHash = _refreshTokenGenerator.HashToken(plainToken),
            ExpiresAtUtc = now.AddMinutes(Math.Max(1, _sessionOptions.TakeoverTokenTtlMinutes)),
            CreatedAtUtc = now,
            ActiveSessionId = activeSession.Id,
            HasActiveTrip = trip is not null,
            ActiveTripId = trip?.Id
        };
        await _takeoverTokens.AddAsync(takeover, cancellationToken);
        return new ActiveSessionConflictResponse(new ActiveSessionResponse(activeSession.SessionType.ToString(), activeSession.DeviceName, activeSession.Platform, activeSession.LastSeenAtUtc), plainToken, takeover.ExpiresAtUtc, trip is not null, trip is null ? null : ToActiveTripResponse(trip));
    }

    private async Task TouchSessionAsync(UserSession session, CancellationToken cancellationToken)
    {
        DateTimeOffset now = _clock.UtcNow;
        if (session.LastSeenAtUtc.AddMinutes(Math.Max(1, _sessionOptions.LastSeenUpdateThrottleMinutes)) > now) return;
        session.LastSeenAtUtc = now;
        session.UpdatedAtUtc = now;
        await _sessions.UpdateAsync(session, cancellationToken);
    }

    private static UserSession CreateSession(User user, UserSessionType sessionType, ClientDeviceRequest? device, DateTimeOffset now, string source) => new()
    {
        UserId = user.Id,
        Role = user.Role.ToString(),
        SessionType = sessionType,
        ClientDeviceId = device?.ClientDeviceId ?? string.Empty,
        DeviceName = device?.DeviceName ?? DefaultDeviceName(sessionType),
        Platform = device?.Platform ?? DefaultPlatform(sessionType),
        OsVersion = device?.OsVersion,
        AppVersion = device?.AppVersion,
        CreatedAtUtc = now,
        LastSeenAtUtc = now,
        UpdatedAtUtc = now,
        TakeoverSource = source
    };

    private static ClientDeviceRequest? NormalizeClientDevice(ClientDeviceRequest? request, UserSessionType sessionType)
    {
        if (request is null || !Guid.TryParse(request.ClientDeviceId, out _) || string.IsNullOrWhiteSpace(request.DeviceName) || string.IsNullOrWhiteSpace(request.Platform)) return null;
        if (sessionType == UserSessionType.MobileApp && (string.IsNullOrWhiteSpace(request.OsVersion) || string.IsNullOrWhiteSpace(request.AppVersion))) return null;
        return new ClientDeviceRequest(request.ClientDeviceId.Trim(), request.DeviceName.Trim(), request.Platform.Trim(), NormalizeOptional(request.OsVersion), NormalizeOptional(request.AppVersion));
    }

    private ClientDeviceRequest? CreateTestingClientDevice(User user, UserSessionType sessionType)
    {
        if (!_environment.IsEnvironment("Testing") || sessionType != UserSessionType.MobileApp) return null;
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(user.Id));
        var id = new Guid(hash[..16]);
        return new ClientDeviceRequest(id.ToString(), "Testing Device", "Android", "Android Testing", "Testing");
    }

    private static UserSessionType DetermineSessionType(User user, ClientDeviceRequest? clientDevice, string? clientType)
    {
        if (clientDevice is not null && IsMobilePlatform(clientDevice.Platform)) return UserSessionType.MobileApp;
        if (Enum.TryParse(clientType, ignoreCase: true, out UserSessionType parsed) && parsed != UserSessionType.Unknown) return parsed;
        return user.Role == UserRole.Admin ? UserSessionType.AdminWeb : UserSessionType.WebApp;
    }

    private static bool IsMobilePlatform(string? platform) => string.Equals(platform?.Trim(), "Android", StringComparison.OrdinalIgnoreCase) || string.Equals(platform?.Trim(), "iOS", StringComparison.OrdinalIgnoreCase);
    private static string DefaultDeviceName(UserSessionType sessionType) => sessionType == UserSessionType.AdminWeb ? "Admin Web" : "Web Browser";
    private static string DefaultPlatform(UserSessionType sessionType) => sessionType == UserSessionType.MobileApp ? "Mobile" : "Web";
    private static SessionResponse ToSessionResponse(UserSession session) => new(session.Id, session.SessionType.ToString(), session.DeviceName, session.Platform, session.CreatedAtUtc, session.LastSeenAtUtc);
    private static ActiveTripSessionResponse ToActiveTripResponse(Trip trip, bool transferred = false) => new(trip.Id, trip.Status.ToString(), trip.StartedAtUtc, trip.MobileDeviceId, transferred);

    private void EnsureAuthCodesEnabled()
    {
        if (!_authCodeOptions.Enabled)
        {
            throw new FeatureDisabledAppException("Authentication codes are disabled.");
        }
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
