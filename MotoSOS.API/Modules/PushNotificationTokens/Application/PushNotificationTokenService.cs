using MongoDB.Bson;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AuditLogs.Application;
using MotoSOS.API.Modules.AuditLogs.Domain;
using MotoSOS.API.Modules.Devices.Application;
using MotoSOS.API.Modules.Devices.Domain;
using MotoSOS.API.Modules.PushNotificationTokens.Contracts;
using MotoSOS.API.Modules.PushNotificationTokens.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Modules.PushNotificationTokens.Application;

public sealed class PushNotificationTokenService : IPushNotificationTokenService
{
    private const string EntityType = "PushNotificationToken";
    private readonly IUserRepository _users;
    private readonly IUserDeviceRepository _devices;
    private readonly IPushNotificationTokenRepository _tokens;
    private readonly IPushNotificationTokenHasher _hasher;
    private readonly IPushNotificationTokenPreviewer _previewer;
    private readonly IPushNotificationTokenIdempotencyKeyFactory _idempotencyKeys;
    private readonly IClock _clock;
    private readonly IAuditLogService? _auditLogs;

    public PushNotificationTokenService(IUserRepository users, IUserDeviceRepository devices, IPushNotificationTokenRepository tokens, IPushNotificationTokenHasher hasher, IPushNotificationTokenPreviewer previewer, IPushNotificationTokenIdempotencyKeyFactory idempotencyKeys, IClock clock, IAuditLogService? auditLogs = null)
    {
        _users = users;
        _devices = devices;
        _tokens = tokens;
        _hasher = hasher;
        _previewer = previewer;
        _idempotencyKeys = idempotencyKeys;
        _clock = clock;
        _auditLogs = auditLogs;
    }

    public Task<RegisterPushNotificationTokenResponse> RegisterAsync(string userId, ValidatedRegisterPushNotificationTokenRequest request, CancellationToken cancellationToken) => RegisterAsync(userId, null, request, cancellationToken);

    public async Task<RegisterPushNotificationTokenResponse> RegisterAsync(string userId, string? sessionId, ValidatedRegisterPushNotificationTokenRequest request, CancellationToken cancellationToken)
    {
        User user = await GetActiveUserAsync(userId, cancellationToken);
        await EnsureOwnedDeviceAsync(user.Id, request.DeviceId, cancellationToken);

        DateTimeOffset now = _clock.UtcNow;
        string tokenHash = _hasher.Hash(request.Token);
        string idempotencyKey = _idempotencyKeys.Create(user.Id, tokenHash, request.Platform, request.Channel, request.DeviceId);
        PushNotificationToken? existing = await _tokens.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            existing.LastSeenAtUtc = now;
            existing.UpdatedAtUtc = now;
            existing.Status = PushNotificationTokenStatus.Active;
            existing.RevokedAtUtc = null;
            existing.SessionId = sessionId;
            existing.Metadata = request.Metadata.ToDictionary(item => item.Key, item => item.Value);
            await _tokens.UpdateAsync(existing, cancellationToken);
            await RecordAsync(user, existing, AuditAction.PushNotificationTokenRegistered, cancellationToken);
            return new RegisterPushNotificationTokenResponse(ToResponse(existing));
        }

        await _tokens.RevokeActiveTokensForScopeAsync(user.Id, request.Platform, request.Channel, request.DeviceId, now, cancellationToken);
        var token = new PushNotificationToken
        {
            Id = ObjectId.GenerateNewId().ToString(),
            UserId = user.Id,
            SessionId = sessionId,
            DeviceId = request.DeviceId,
            Platform = request.Platform,
            Channel = request.Channel,
            TokenHash = tokenHash,
            TokenValue = request.Token,
            TokenPreview = _previewer.CreatePreview(request.Token),
            Status = PushNotificationTokenStatus.Active,
            RegisteredAtUtc = now,
            LastSeenAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            IdempotencyKey = idempotencyKey,
            Metadata = request.Metadata.ToDictionary(item => item.Key, item => item.Value)
        };

        (PushNotificationToken saved, bool duplicate) = await _tokens.AddOrGetDuplicateAsync(token, cancellationToken);
        if (duplicate)
        {
            saved.LastSeenAtUtc = now;
            saved.UpdatedAtUtc = now;
            saved.Status = PushNotificationTokenStatus.Active;
            saved.RevokedAtUtc = null;
            await _tokens.UpdateAsync(saved, cancellationToken);
        }

        await RecordAsync(user, saved, AuditAction.PushNotificationTokenRegistered, cancellationToken);
        return new RegisterPushNotificationTokenResponse(ToResponse(saved));
    }

    public async Task<GetPushNotificationTokensResponse> ListMineAsync(string userId, PushNotificationTokenQuery query, CancellationToken cancellationToken)
    {
        User user = await GetActiveUserAsync(userId, cancellationToken);
        IReadOnlyList<PushNotificationToken> items = await _tokens.ListByUserIdAsync(user.Id, query, cancellationToken);
        long total = await _tokens.CountByUserIdAsync(user.Id, query, cancellationToken);
        return new GetPushNotificationTokensResponse(items.Select(ToResponse).ToArray(), query.PageNumber, query.PageSize, total);
    }

    public async Task<PushNotificationTokenStatusResponse> GetStatusAsync(string userId, CancellationToken cancellationToken)
    {
        User user = await GetActiveUserAsync(userId, cancellationToken);
        PushNotificationTokenStatusSummary summary = await _tokens.GetStatusByUserIdAsync(user.Id, cancellationToken);
        return new PushNotificationTokenStatusResponse(summary.ActiveTokenCount, summary.RevokedTokenCount, summary.HasActiveAndroidFcm, summary.HasActiveIosApns, summary.HasActiveWebPush, summary.HasActiveWebFcm, summary.LastRegisteredAtUtc);
    }

    public async Task<PushNotificationTokenResponse> RevokeMineAsync(string userId, string id, CancellationToken cancellationToken)
    {
        User user = await GetActiveUserAsync(userId, cancellationToken);
        PushNotificationToken token = await GetOwnedTokenAsync(user.Id, id, cancellationToken);
        await RevokeAsync(token, cancellationToken);
        await RecordAsync(user, token, AuditAction.PushNotificationTokenRevoked, cancellationToken);
        return ToResponse(token);
    }

    public async Task<GetPushNotificationTokensResponse> ListForAdminAsync(string adminUserId, PushNotificationTokenQuery query, CancellationToken cancellationToken)
    {
        await EnsureAdminAsync(adminUserId, cancellationToken);
        IReadOnlyList<PushNotificationToken> items = await _tokens.ListAsync(query, cancellationToken);
        long total = await _tokens.CountAsync(query, cancellationToken);
        return new GetPushNotificationTokensResponse(items.Select(ToResponse).ToArray(), query.PageNumber, query.PageSize, total);
    }

    public async Task<PushNotificationTokenResponse> RevokeForAdminAsync(string adminUserId, string id, CancellationToken cancellationToken)
    {
        User admin = await EnsureAdminAsync(adminUserId, cancellationToken);
        PushNotificationToken token = await _tokens.GetByIdAsync(id.Trim(), cancellationToken) ?? throw new PushNotificationTokenNotAvailableAppException("Push notification token was not found.");
        await RevokeAsync(token, cancellationToken);
        await RecordAsync(admin, token, AuditAction.PushNotificationTokenRevoked, cancellationToken);
        return ToResponse(token);
    }

    private async Task<User> GetActiveUserAsync(string userId, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive) throw new UnauthorizedAppException("Invalid authentication credentials.");
        if (user.Role is not (UserRole.Rider or UserRole.Monitor or UserRole.Admin)) throw new ForbiddenAppException("Push Notification Tokens API is not available for this role.");
        return user;
    }

    private async Task<User> EnsureAdminAsync(string userId, CancellationToken cancellationToken)
    {
        User user = await GetActiveUserAsync(userId, cancellationToken);
        if (user.Role != UserRole.Admin) throw new ForbiddenAppException("Push Notification Tokens admin API is available only for admins.");
        return user;
    }

    private async Task EnsureOwnedDeviceAsync(string userId, string? deviceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deviceId)) return;
        UserDevice? device = await _devices.GetByIdAsync(deviceId, cancellationToken);
        if (device is null || device.UserId != userId || !device.IsActive || device.LinkStatus != DeviceLinkStatus.Linked || device.RevokedAtUtc is not null)
        {
            throw new PushNotificationTokenNotAllowedAppException("Device cannot be used for push notification tokens.");
        }
    }

    private async Task<PushNotificationToken> GetOwnedTokenAsync(string userId, string id, CancellationToken cancellationToken)
    {
        PushNotificationToken? token = await _tokens.GetByIdAsync(id.Trim(), cancellationToken);
        if (token is null || token.UserId != userId) throw new PushNotificationTokenNotAvailableAppException("Push notification token was not found.");
        return token;
    }

    private async Task RevokeAsync(PushNotificationToken token, CancellationToken cancellationToken)
    {
        if (token.Status == PushNotificationTokenStatus.Revoked) return;
        DateTimeOffset now = _clock.UtcNow;
        token.Status = PushNotificationTokenStatus.Revoked;
        token.RevokedAtUtc = now;
        token.UpdatedAtUtc = now;
        await _tokens.UpdateAsync(token, cancellationToken);
    }

    private async Task RecordAsync(User actor, PushNotificationToken token, AuditAction action, CancellationToken cancellationToken)
    {
        try
        {
            if (_auditLogs is null) return;
            await _auditLogs.RecordAsync(
                actor.Id,
                actor.Role.ToString(),
                action,
                AuditModule.Notifications,
                EntityType,
                token.Id,
                AuditOutcome.Success,
                null,
                null,
                null,
                new Dictionary<string, string>
                {
                    ["pushNotificationTokenId"] = token.Id,
                    ["platform"] = token.Platform.ToString(),
                    ["channel"] = token.Channel.ToString(),
                    ["deviceId"] = token.DeviceId ?? string.Empty,
                    ["status"] = token.Status.ToString()
                },
                cancellationToken);
        }
        catch
        {
        }
    }

    private static PushNotificationTokenResponse ToResponse(PushNotificationToken token) => new(
        token.Id,
        token.Platform.ToString(),
        token.Channel.ToString(),
        token.DeviceId,
        token.TokenPreview,
        token.Status.ToString(),
        token.RegisteredAtUtc,
        token.LastSeenAtUtc,
        token.RevokedAtUtc);
}
