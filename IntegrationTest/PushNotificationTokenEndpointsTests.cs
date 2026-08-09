using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MotoSOS.API.Modules.Auth.Application;
using MotoSOS.API.Modules.Auth.Contracts;
using MotoSOS.API.Modules.Auth.Domain;
using MotoSOS.API.Modules.Devices.Application;
using MotoSOS.API.Modules.Devices.Domain;
using MotoSOS.API.Modules.PushNotificationTokens.Application;
using MotoSOS.API.Modules.PushNotificationTokens.Contracts;
using MotoSOS.API.Modules.PushNotificationTokens.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace IntegrationTest;

public sealed class PushNotificationTokenEndpointsTests
{
    [Fact]
    public async Task EndpointsRequireAuthentication()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory(new Stores());
        HttpClient client = factory.CreateClient();

        (await client.PostAsJsonAsync("/api/v1/push-notification-tokens", Request(Token))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/push-notification-tokens")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/push-notification-tokens/status")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsync("/api/v1/push-notification-tokens/id/revoke", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/admin/push-notification-tokens")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RiderMonitorAndAdminCanRegisterOwnTokens()
    {
        var stores = new Stores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient rider = factory.CreateClient(); HttpClient monitor = factory.CreateClient(); HttpClient admin = factory.CreateClient();
        await AuthenticateAsync(rider, "push-rider@example.com", UserRole.Rider, stores);
        await AuthenticateAsync(monitor, "push-monitor@example.com", UserRole.Monitor, stores);
        await AuthenticateAsync(admin, "push-admin@example.com", UserRole.Admin, stores);

        RegisterEnvelope riderResponse = (await (await rider.PostAsJsonAsync("/api/v1/push-notification-tokens", Request(Token))).Content.ReadFromJsonAsync<RegisterEnvelope>())!;
        RegisterEnvelope monitorResponse = (await (await monitor.PostAsJsonAsync("/api/v1/push-notification-tokens", new { platform = "Web", channel = "WebPush", token = "monitor-web-token-abcdefghijkl" })).Content.ReadFromJsonAsync<RegisterEnvelope>())!;
        RegisterEnvelope adminResponse = (await (await admin.PostAsJsonAsync("/api/v1/push-notification-tokens", new { platform = "Ios", channel = "Apns", token = "admin-ios-token-abcdefghijkl" })).Content.ReadFromJsonAsync<RegisterEnvelope>())!;

        riderResponse.Data.PushNotificationToken.Channel.Should().Be("Fcm");
        monitorResponse.Data.PushNotificationToken.Channel.Should().Be("WebPush");
        adminResponse.Data.PushNotificationToken.Channel.Should().Be("Apns");
    }

    [Fact]
    public async Task ForbiddenBodyPropertiesAndInvalidCombinationsReturnValidationError()
    {
        var stores = new Stores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient rider = factory.CreateClient(); await AuthenticateAsync(rider, "push-rider2@example.com", UserRole.Rider, stores);

        HttpResponseMessage withUserId = await rider.PostAsJsonAsync("/api/v1/push-notification-tokens", new { platform = "Android", channel = "Fcm", token = Token, userId = "other" });
        HttpResponseMessage shortToken = await rider.PostAsJsonAsync("/api/v1/push-notification-tokens", new { platform = "Android", channel = "Fcm", token = "short" });
        HttpResponseMessage badCombination = await rider.PostAsJsonAsync("/api/v1/push-notification-tokens", new { platform = "Android", channel = "Apns", token = Token });

        withUserId.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await withUserId.Content.ReadAsStringAsync()).Should().Contain("validation_error");
        shortToken.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        badCombination.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SameTokenIsIdempotentAndNewTokenSameScopeRevokesPrevious()
    {
        var stores = new Stores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient rider = factory.CreateClient(); await AuthenticateAsync(rider, "push-rider3@example.com", UserRole.Rider, stores);

        RegisterEnvelope first = (await (await rider.PostAsJsonAsync("/api/v1/push-notification-tokens", Request(Token))).Content.ReadFromJsonAsync<RegisterEnvelope>())!;
        RegisterEnvelope duplicate = (await (await rider.PostAsJsonAsync("/api/v1/push-notification-tokens", Request(Token))).Content.ReadFromJsonAsync<RegisterEnvelope>())!;
        RegisterEnvelope replacement = (await (await rider.PostAsJsonAsync("/api/v1/push-notification-tokens", Request("zzzzzz1234567890yyyy"))).Content.ReadFromJsonAsync<RegisterEnvelope>())!;

        duplicate.Data.PushNotificationToken.Id.Should().Be(first.Data.PushNotificationToken.Id);
        stores.Tokens.Items.Should().HaveCount(2);
        stores.Tokens.Items.Should().Contain(token => token.Id == first.Data.PushNotificationToken.Id && token.Status == PushNotificationTokenStatus.Revoked);
        replacement.Data.PushNotificationToken.Status.Should().Be("Active");
    }

    [Fact]
    public async Task ListStatusRevokeAndAdminOperationsWorkWithoutExposingTokenValues()
    {
        var stores = new Stores();
        stores.Devices.Items.Add(new UserDevice { Id = "device-1", UserId = "", IsActive = true, LinkStatus = DeviceLinkStatus.Linked });
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient rider = factory.CreateClient(); HttpClient other = factory.CreateClient(); HttpClient admin = factory.CreateClient();
        User riderUser = await AuthenticateAsync(rider, "push-rider4@example.com", UserRole.Rider, stores);
        await AuthenticateAsync(other, "push-other@example.com", UserRole.Rider, stores);
        await AuthenticateAsync(admin, "push-admin4@example.com", UserRole.Admin, stores);
        stores.Devices.Items[0].UserId = riderUser.Id;

        RegisterEnvelope registered = (await (await rider.PostAsJsonAsync("/api/v1/push-notification-tokens", Request(Token, "device-1"))).Content.ReadFromJsonAsync<RegisterEnvelope>())!;
        string listBody = await (await rider.GetAsync("/api/v1/push-notification-tokens")).Content.ReadAsStringAsync();
        StatusEnvelope status = (await (await rider.GetAsync("/api/v1/push-notification-tokens/status")).Content.ReadFromJsonAsync<StatusEnvelope>())!;
        HttpResponseMessage otherRevoke = await other.PostAsync($"/api/v1/push-notification-tokens/{registered.Data.PushNotificationToken.Id}/revoke", null);
        TokenEnvelope revoked = (await (await rider.PostAsync($"/api/v1/push-notification-tokens/{registered.Data.PushNotificationToken.Id}/revoke", null)).Content.ReadFromJsonAsync<TokenEnvelope>())!;
        HttpResponseMessage adminListForRider = await rider.GetAsync("/api/v1/admin/push-notification-tokens");
        GetEnvelope adminList = (await (await admin.GetAsync("/api/v1/admin/push-notification-tokens")).Content.ReadFromJsonAsync<GetEnvelope>())!;
        TokenEnvelope adminRevoked = (await (await admin.PostAsync($"/api/v1/admin/push-notification-tokens/{registered.Data.PushNotificationToken.Id}/revoke", null)).Content.ReadFromJsonAsync<TokenEnvelope>())!;

        listBody.Should().Contain("abcdef****wxyz").And.NotContain(Token);
        listBody.ToLowerInvariant().Should().NotContain("tokenhash").And.NotContain("tokenvalue");
        status.Data.ActiveTokenCount.Should().Be(1);
        status.Data.HasActiveAndroidFcm.Should().BeTrue();
        otherRevoke.StatusCode.Should().Be(HttpStatusCode.NotFound);
        revoked.Data.Status.Should().Be("Revoked");
        adminListForRider.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        adminList.Data.PushNotificationTokens.Should().ContainSingle(token => token.Id == registered.Data.PushNotificationToken.Id);
        adminRevoked.Data.Status.Should().Be("Revoked");
    }

    private const string Token = "abcdef1234567890wxyz";
    private static object Request(string token, string? deviceId = null) => new { platform = "Android", channel = "Fcm", deviceId, token, metadata = new Dictionary<string, string> { ["appVersion"] = "1.0.0" } };
    private static async Task<User> AuthenticateAsync(HttpClient client, string email, UserRole role, Stores stores) { const string secret = "StrongPass1!"; await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, secret, secret, "Moto User", null, role == UserRole.Monitor ? "Monitor" : "Rider", true)); User user = stores.Users.Items.Single(u => u.Email == email); user.Role = role; LoginEnvelope login = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, secret))).Content.ReadFromJsonAsync<LoginEnvelope>())!; client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Data.AccessToken); return user; }
    private static WebApplicationFactory<Program> CreateFactory(Stores stores) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Issuer"] = "MotoSOS", ["Jwt:Audience"] = "MotoSOS.Clients", ["Jwt:Key"] = new string('A', 48), ["Jwt:AccessTokenMinutes"] = "15", ["Jwt:RefreshTokenDays"] = "7", ["Jwt:RefreshTokenRememberMeDays"] = "30", ["MongoDb:" + "Connection" + "String"] = string.Empty, ["MongoDb:DatabaseName"] = "MotoSOS_Test" })); builder.ConfigureTestServices(services => { services.AddSingleton<IUserRepository>(stores.Users); services.AddSingleton<IRefreshTokenRepository>(stores.RefreshTokens); services.AddSingleton<IUserDeviceRepository>(stores.Devices); services.AddSingleton<IPushNotificationTokenRepository>(stores.Tokens); }); });
    private sealed record LoginEnvelope(bool Success, LoginResponse Data);
    private sealed record RegisterEnvelope(bool Success, RegisterPushNotificationTokenResponse Data);
    private sealed record GetEnvelope(bool Success, GetPushNotificationTokensResponse Data);
    private sealed record StatusEnvelope(bool Success, PushNotificationTokenStatusResponse Data);
    private sealed record TokenEnvelope(bool Success, PushNotificationTokenResponse Data);
    private sealed class Stores { public Users Users { get; } = new(); public RefreshTokens RefreshTokens { get; } = new(); public Devices Devices { get; } = new(); public Tokens Tokens { get; } = new(); }
    private sealed class Users : IUserRepository { public List<User> Items { get; } = []; public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Id == id)); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Email == email)); public Task AddAsync(User u, CancellationToken ct) { Items.Add(u); return Task.CompletedTask; } public Task UpdateAsync(User u, CancellationToken ct) => Task.CompletedTask; }
    private sealed class RefreshTokens : IRefreshTokenRepository { public List<RefreshToken> Items { get; } = []; public Task<RefreshToken?> GetByHashAsync(string h, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.TokenHash == h)); public Task AddAsync(RefreshToken t, CancellationToken ct) { Items.Add(t); return Task.CompletedTask; } public Task UpdateAsync(RefreshToken t, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Devices : IUserDeviceRepository { public List<UserDevice> Items { get; } = []; public Task<IReadOnlyList<UserDevice>> GetActiveByUserIdAsync(string userId, CancellationToken ct) => Task.FromResult<IReadOnlyList<UserDevice>>(Items.Where(d => d.UserId == userId && d.IsActive).ToArray()); public Task<IReadOnlyList<UserDevice>> GetActiveByParentDeviceIdAsync(string parentDeviceId, CancellationToken ct) => Task.FromResult<IReadOnlyList<UserDevice>>([]); public Task<UserDevice?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(d => d.Id == id)); public Task<UserDevice?> GetByDeviceIdentifierHashAsync(string userId, string hash, DeviceType deviceType, CancellationToken ct) => Task.FromResult<UserDevice?>(null); public Task<int> CountActiveLinkedByUserIdAndTypeAsync(string userId, DeviceType deviceType, CancellationToken ct) => Task.FromResult(0); public Task<bool> HasActiveLinkedMobileAppAsync(string userId, CancellationToken ct) => Task.FromResult(true); public Task AddAsync(UserDevice device, CancellationToken ct) { Items.Add(device); return Task.CompletedTask; } public Task UpdateAsync(UserDevice device, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Tokens : IPushNotificationTokenRepository { public List<PushNotificationToken> Items { get; } = []; public Task<PushNotificationToken?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.Id == id)); public Task<PushNotificationToken?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.IdempotencyKey == key)); public Task<(PushNotificationToken Token, bool IsDuplicate)> AddOrGetDuplicateAsync(PushNotificationToken token, CancellationToken ct) { PushNotificationToken? existing = Items.FirstOrDefault(t => t.IdempotencyKey == token.IdempotencyKey); if (existing is not null) return Task.FromResult((existing, true)); Items.Add(token); return Task.FromResult((token, false)); } public Task UpdateAsync(PushNotificationToken token, CancellationToken ct) => Task.CompletedTask; public Task<long> RevokeActiveTokensForScopeAsync(string userId, PushTokenPlatform platform, PushTokenChannel channel, string? deviceId, DateTimeOffset now, CancellationToken ct) { long count = 0; foreach (PushNotificationToken token in Items.Where(t => t.UserId == userId && t.Platform == platform && t.Channel == channel && t.DeviceId == deviceId && t.Status == PushNotificationTokenStatus.Active)) { token.Status = PushNotificationTokenStatus.Revoked; token.RevokedAtUtc = now; token.UpdatedAtUtc = now; count++; } return Task.FromResult(count); } public Task<IReadOnlyList<PushNotificationToken>> ListByUserIdAsync(string userId, PushNotificationTokenQuery q, CancellationToken ct) => Task.FromResult<IReadOnlyList<PushNotificationToken>>(Apply(q).Where(t => t.UserId == userId).ToArray()); public Task<long> CountByUserIdAsync(string userId, PushNotificationTokenQuery q, CancellationToken ct) => Task.FromResult((long)Apply(q).Count(t => t.UserId == userId)); public Task<PushNotificationTokenStatusSummary> GetStatusByUserIdAsync(string userId, CancellationToken ct) { PushNotificationToken[] items = Items.Where(t => t.UserId == userId).ToArray(); PushNotificationToken[] active = items.Where(t => t.Status == PushNotificationTokenStatus.Active).ToArray(); return Task.FromResult(new PushNotificationTokenStatusSummary(active.Length, items.Count(t => t.Status == PushNotificationTokenStatus.Revoked), active.Any(t => t.Platform == PushTokenPlatform.Android && t.Channel == PushTokenChannel.Fcm), active.Any(t => t.Platform == PushTokenPlatform.Ios && t.Channel == PushTokenChannel.Apns), active.Any(t => t.Platform == PushTokenPlatform.Web && t.Channel == PushTokenChannel.WebPush), active.Any(t => t.Platform == PushTokenPlatform.Web && t.Channel == PushTokenChannel.Fcm), items.OrderByDescending(t => t.RegisteredAtUtc).FirstOrDefault()?.RegisteredAtUtc)); } public Task<IReadOnlyList<PushNotificationToken>> ListAsync(PushNotificationTokenQuery q, CancellationToken ct) => Task.FromResult<IReadOnlyList<PushNotificationToken>>(Apply(q).ToArray()); public Task<long> CountAsync(PushNotificationTokenQuery q, CancellationToken ct) => Task.FromResult((long)Apply(q).Count()); private IEnumerable<PushNotificationToken> Apply(PushNotificationTokenQuery q) => Items.Where(t => (q.UserId is null || t.UserId == q.UserId) && (!q.Platform.HasValue || t.Platform == q.Platform) && (!q.Channel.HasValue || t.Channel == q.Channel) && (!q.Status.HasValue || t.Status == q.Status)); }
}
