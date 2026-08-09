using FluentAssertions;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AuditLogs.Application;
using MotoSOS.API.Modules.AuditLogs.Contracts;
using MotoSOS.API.Modules.AuditLogs.Domain;
using MotoSOS.API.Modules.Devices.Application;
using MotoSOS.API.Modules.Devices.Domain;
using MotoSOS.API.Modules.PushNotificationTokens.Application;
using MotoSOS.API.Modules.PushNotificationTokens.Contracts;
using MotoSOS.API.Modules.PushNotificationTokens.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace UnitTest.PushNotificationTokens;

public sealed class PushNotificationTokenServiceTests
{
    [Fact]
    public async Task UserRegistersTokenWithoutExposingSensitiveValues()
    {
        Ctx c = Ctx.Create();

        RegisterPushNotificationTokenResponse response = await c.Service.RegisterAsync("rider", Request(Token), CancellationToken.None);

        response.PushNotificationToken.Platform.Should().Be("Android");
        response.PushNotificationToken.Channel.Should().Be("Fcm");
        response.PushNotificationToken.TokenPreview.Should().Be("abcdef****wxyz");
        response.PushNotificationToken.TokenPreview.Should().NotBe(Token);
        c.Tokens.Items.Should().ContainSingle(token => token.TokenValue == Token && !string.IsNullOrWhiteSpace(token.TokenHash));
        c.Audit.Metadata.Keys.Should().Contain("pushNotificationTokenId");
        c.Audit.Metadata.Keys.Should().NotContain(key => key.Equals("tokenHash", StringComparison.OrdinalIgnoreCase) || key.Equals("tokenValue", StringComparison.OrdinalIgnoreCase) || key.Equals("tokenPreview", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SameTokenIsIdempotentAndUpdatesLastSeenButPreservesCreatedAt()
    {
        Ctx c = Ctx.Create();
        RegisterPushNotificationTokenResponse first = await c.Service.RegisterAsync("rider", Request(Token), CancellationToken.None);
        c.Clock.Now = Now.AddMinutes(5);

        RegisterPushNotificationTokenResponse second = await c.Service.RegisterAsync("rider", Request(Token), CancellationToken.None);

        second.PushNotificationToken.Id.Should().Be(first.PushNotificationToken.Id);
        c.Tokens.Items.Should().ContainSingle();
        c.Tokens.Items[0].CreatedAtUtc.Should().Be(Now);
        c.Tokens.Items[0].LastSeenAtUtc.Should().Be(Now.AddMinutes(5));
    }

    [Fact]
    public async Task NewTokenForSameScopeRevokesPreviousActiveToken()
    {
        Ctx c = Ctx.Create();
        await c.Service.RegisterAsync("rider", Request(Token, deviceId: null), CancellationToken.None);
        c.Clock.Now = Now.AddMinutes(1);

        await c.Service.RegisterAsync("rider", Request("zzzzzz1234567890yyyy", deviceId: null), CancellationToken.None);

        c.Tokens.Items.Should().HaveCount(2);
        c.Tokens.Items.Should().Contain(token => token.Status == PushNotificationTokenStatus.Revoked && token.RevokedAtUtc == Now.AddMinutes(1));
        c.Tokens.Items.Should().Contain(token => token.Status == PushNotificationTokenStatus.Active && token.TokenValue.StartsWith("zzzzzz", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DeviceOwnershipIsValidated()
    {
        Ctx c = Ctx.Create();
        c.Devices.Items.Add(Device("device", "rider"));
        await c.Service.RegisterAsync("rider", Request(Token, "device"), CancellationToken.None);

        await Assert.ThrowsAsync<PushNotificationTokenNotAllowedAppException>(() => c.Service.RegisterAsync("rider", Request(Token, "other"), CancellationToken.None));
        c.Devices.Items.Add(Device("foreign", "other"));
        await Assert.ThrowsAsync<PushNotificationTokenNotAllowedAppException>(() => c.Service.RegisterAsync("rider", Request(Token, "foreign"), CancellationToken.None));
    }

    [Fact]
    public async Task ListStatusAndRevokeWorkForOwnTokensAndAdmin()
    {
        Ctx c = Ctx.Create();
        RegisterPushNotificationTokenResponse own = await c.Service.RegisterAsync("rider", Request(Token), CancellationToken.None);
        await c.Service.RegisterAsync("monitor", new ValidatedRegisterPushNotificationTokenRequest(PushTokenPlatform.Web, PushTokenChannel.WebPush, null, "monitor-token-abcdefghijkl", new Dictionary<string, string>()), CancellationToken.None);

        GetPushNotificationTokensResponse list = await c.Service.ListMineAsync("rider", new PushNotificationTokenQuery(null, null, null, null, null, null), CancellationToken.None);
        PushNotificationTokenStatusResponse status = await c.Service.GetStatusAsync("rider", CancellationToken.None);
        PushNotificationTokenResponse revoked = await c.Service.RevokeMineAsync("rider", own.PushNotificationToken.Id, CancellationToken.None);
        GetPushNotificationTokensResponse adminList = await c.Service.ListForAdminAsync("admin", new PushNotificationTokenQuery(null, null, null, null, null, null), CancellationToken.None);

        list.PushNotificationTokens.Should().ContainSingle(token => token.Id == own.PushNotificationToken.Id);
        status.ActiveTokenCount.Should().Be(1);
        status.HasActiveAndroidFcm.Should().BeTrue();
        revoked.Status.Should().Be("Revoked");
        adminList.PushNotificationTokens.Should().HaveCount(2);
        await Assert.ThrowsAsync<PushNotificationTokenNotAvailableAppException>(() => c.Service.RevokeMineAsync("monitor", own.PushNotificationToken.Id, CancellationToken.None));
        await c.Service.RevokeForAdminAsync("admin", own.PushNotificationToken.Id, CancellationToken.None);
    }

    [Fact]
    public async Task AuditWriteFailureDoesNotBreakOperation()
    {
        Ctx c = Ctx.Create(failingAudit: true);

        RegisterPushNotificationTokenResponse response = await c.Service.RegisterAsync("rider", Request(Token), CancellationToken.None);

        response.PushNotificationToken.Status.Should().Be("Active");
    }

    private const string Token = "abcdef1234567890wxyz";
    private static readonly DateTimeOffset Now = new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);
    private static ValidatedRegisterPushNotificationTokenRequest Request(string token, string? deviceId = null) => new(PushTokenPlatform.Android, PushTokenChannel.Fcm, deviceId, token, new Dictionary<string, string>());
    private static UserDevice Device(string id, string userId) => new() { Id = id, UserId = userId, IsActive = true, LinkStatus = DeviceLinkStatus.Linked, RevokedAtUtc = null };
    private sealed class Clock : IClock { public DateTimeOffset Now { get; set; } = PushNotificationTokenServiceTests.Now; public DateTimeOffset UtcNow => Now; }
    private sealed class Users : IUserRepository { public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult<User?>(id switch { "rider" => new User { Id = "rider", Role = UserRole.Rider, IsActive = true }, "monitor" => new User { Id = "monitor", Role = UserRole.Monitor, IsActive = true }, "admin" => new User { Id = "admin", Role = UserRole.Admin, IsActive = true }, _ => null }); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult<User?>(null); public Task AddAsync(User user, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(User user, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Devices : IUserDeviceRepository { public List<UserDevice> Items { get; } = []; public Task<IReadOnlyList<UserDevice>> GetActiveByUserIdAsync(string userId, CancellationToken ct) => Task.FromResult<IReadOnlyList<UserDevice>>(Items.Where(d => d.UserId == userId && d.IsActive).ToArray()); public Task<IReadOnlyList<UserDevice>> GetActiveByParentDeviceIdAsync(string parentDeviceId, CancellationToken ct) => Task.FromResult<IReadOnlyList<UserDevice>>([]); public Task<UserDevice?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(d => d.Id == id)); public Task<UserDevice?> GetByDeviceIdentifierHashAsync(string userId, string hash, DeviceType deviceType, CancellationToken ct) => Task.FromResult<UserDevice?>(null); public Task<int> CountActiveLinkedByUserIdAndTypeAsync(string userId, DeviceType deviceType, CancellationToken ct) => Task.FromResult(0); public Task<bool> HasActiveLinkedMobileAppAsync(string userId, CancellationToken ct) => Task.FromResult(true); public Task AddAsync(UserDevice device, CancellationToken ct) { Items.Add(device); return Task.CompletedTask; } public Task UpdateAsync(UserDevice device, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Tokens : IPushNotificationTokenRepository { public List<PushNotificationToken> Items { get; } = []; public Task<PushNotificationToken?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.Id == id)); public Task<PushNotificationToken?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.IdempotencyKey == key)); public Task<(PushNotificationToken Token, bool IsDuplicate)> AddOrGetDuplicateAsync(PushNotificationToken token, CancellationToken ct) { PushNotificationToken? existing = Items.FirstOrDefault(t => t.IdempotencyKey == token.IdempotencyKey); if (existing is not null) return Task.FromResult((existing, true)); Items.Add(token); return Task.FromResult((token, false)); } public Task UpdateAsync(PushNotificationToken token, CancellationToken ct) => Task.CompletedTask; public Task<long> RevokeActiveTokensForScopeAsync(string userId, PushTokenPlatform platform, PushTokenChannel channel, string? deviceId, DateTimeOffset now, CancellationToken ct) { long count = 0; foreach (PushNotificationToken token in Items.Where(t => t.UserId == userId && t.Platform == platform && t.Channel == channel && t.DeviceId == deviceId && t.Status == PushNotificationTokenStatus.Active)) { token.Status = PushNotificationTokenStatus.Revoked; token.RevokedAtUtc = now; token.UpdatedAtUtc = now; count++; } return Task.FromResult(count); } public Task<IReadOnlyList<PushNotificationToken>> ListByUserIdAsync(string userId, PushNotificationTokenQuery query, CancellationToken ct) => Task.FromResult<IReadOnlyList<PushNotificationToken>>(Apply(query).Where(t => t.UserId == userId).ToArray()); public Task<long> CountByUserIdAsync(string userId, PushNotificationTokenQuery query, CancellationToken ct) => Task.FromResult((long)Apply(query).Count(t => t.UserId == userId)); public Task<PushNotificationTokenStatusSummary> GetStatusByUserIdAsync(string userId, CancellationToken ct) { PushNotificationToken[] items = Items.Where(t => t.UserId == userId).ToArray(); PushNotificationToken[] active = items.Where(t => t.Status == PushNotificationTokenStatus.Active).ToArray(); return Task.FromResult(new PushNotificationTokenStatusSummary(active.Length, items.Count(t => t.Status == PushNotificationTokenStatus.Revoked), active.Any(t => t.Platform == PushTokenPlatform.Android && t.Channel == PushTokenChannel.Fcm), active.Any(t => t.Platform == PushTokenPlatform.Ios && t.Channel == PushTokenChannel.Apns), active.Any(t => t.Platform == PushTokenPlatform.Web && t.Channel == PushTokenChannel.WebPush), active.Any(t => t.Platform == PushTokenPlatform.Web && t.Channel == PushTokenChannel.Fcm), items.OrderByDescending(t => t.RegisteredAtUtc).FirstOrDefault()?.RegisteredAtUtc)); } public Task<IReadOnlyList<PushNotificationToken>> ListAsync(PushNotificationTokenQuery query, CancellationToken ct) => Task.FromResult<IReadOnlyList<PushNotificationToken>>(Apply(query).ToArray()); public Task<long> CountAsync(PushNotificationTokenQuery query, CancellationToken ct) => Task.FromResult((long)Apply(query).Count()); private IEnumerable<PushNotificationToken> Apply(PushNotificationTokenQuery q) => Items.Where(t => (q.UserId is null || t.UserId == q.UserId) && (!q.Platform.HasValue || t.Platform == q.Platform) && (!q.Channel.HasValue || t.Channel == q.Channel) && (!q.Status.HasValue || t.Status == q.Status)); }
    private class Audit : IAuditLogService { public IReadOnlyDictionary<string, string> Metadata { get; private set; } = new Dictionary<string, string>(); public virtual Task RecordAsync(string actorUserId, string actorRole, AuditAction action, AuditModule module, string entityType, string? entityId, AuditOutcome outcome, string? reason, string? requestPath, string? httpMethod, IReadOnlyDictionary<string, string>? metadata, CancellationToken cancellationToken) { Metadata = metadata ?? new Dictionary<string, string>(); return Task.CompletedTask; } public Task<GetAuditLogsResponse> ListAsync(string adminUserId, AuditLogQuery query, CancellationToken cancellationToken) => throw new NotImplementedException(); public Task<AuditLogResponse> GetAsync(string adminUserId, string id, CancellationToken cancellationToken) => throw new NotImplementedException(); }
    private sealed class FailingAudit : Audit { public override Task RecordAsync(string actorUserId, string actorRole, AuditAction action, AuditModule module, string entityType, string? entityId, AuditOutcome outcome, string? reason, string? requestPath, string? httpMethod, IReadOnlyDictionary<string, string>? metadata, CancellationToken cancellationToken) => throw new InvalidOperationException("audit failed"); }
    private sealed class Ctx { public Clock Clock { get; private init; } = null!; public Devices Devices { get; private init; } = null!; public Tokens Tokens { get; private init; } = null!; public Audit Audit { get; private init; } = null!; public PushNotificationTokenService Service { get; private init; } = null!; public static Ctx Create(bool failingAudit = false) { var clock = new Clock(); var devices = new Devices(); var tokens = new Tokens(); Audit audit = failingAudit ? new FailingAudit() : new Audit(); return new Ctx { Clock = clock, Devices = devices, Tokens = tokens, Audit = audit, Service = new PushNotificationTokenService(new Users(), devices, tokens, new PushNotificationTokenHasher(), new PushNotificationTokenPreviewer(), new PushNotificationTokenIdempotencyKeyFactory(), clock, audit) }; } }
}
