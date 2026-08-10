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
using MotoSOS.API.Modules.EmergencyContacts.Application;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
using MotoSOS.API.Modules.NotificationOutbox.Contracts;
using MotoSOS.API.Modules.Notifications.Application;
using MotoSOS.API.Modules.Notifications.Contracts;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.Notifications.Providers;
using MotoSOS.API.Modules.PushNotificationTokens.Application;
using MotoSOS.API.Modules.PushNotificationTokens.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace IntegrationTest;

public sealed class FcmNotificationProviderEndpointsTests
{
    [Fact]
    public async Task ProviderStatusRequiresAdmin()
    {
        var stores = new Stores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores, enabled: false); HttpClient client = factory.CreateClient(); HttpClient rider = factory.CreateClient(); HttpClient admin = factory.CreateClient(); await AuthenticateAsync(rider, "fcm-rider@example.com", UserRole.Rider, stores); await AuthenticateAsync(admin, "fcm-admin@example.com", UserRole.Admin, stores);

        (await client.GetAsync("/api/v1/admin/notifications/providers/status")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await rider.GetAsync("/api/v1/admin/notifications/providers/status")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        ProviderStatusEnvelope status = (await (await admin.GetAsync("/api/v1/admin/notifications/providers/status")).Content.ReadFromJsonAsync<ProviderStatusEnvelope>())!;

        status.Data.SimulatedProviderAvailable.Should().BeTrue();
        status.Data.FcmProviderEnabled.Should().BeFalse();
        status.Data.RealPushEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task OutboxRunWithFcmEnabledAndTokenMarksAttemptFcmSent()
    {
        var stores = new Stores();
        stores.Attempts.Items.Add(new NotificationDeliveryAttempt { Id = "attempt", UserId = "rider-owner", AlertDispatchId = "alert", IncidentId = "incident", EmergencyContactId = "contact", Channel = NotificationChannel.Push, Status = NotificationDeliveryStatus.Prepared, PreparedAtUtc = Now, CreatedAtUtc = Now });
        stores.Contacts.Items.Add(new EmergencyContact { Id = "contact", UserId = "rider-owner", IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Linked, LinkedUserId = "monitor-user" });
        stores.Tokens.Items.Add(new PushNotificationToken { Id = "push-token", UserId = "monitor-user", Channel = PushTokenChannel.Fcm, Platform = PushTokenPlatform.Android, Status = PushNotificationTokenStatus.Active, TokenValue = "internal-fcm-token", LastSeenAtUtc = Now });
        await using WebApplicationFactory<Program> factory = CreateFactory(stores, enabled: true);
        HttpClient admin = factory.CreateClient(); await AuthenticateAsync(admin, "fcm-admin2@example.com", UserRole.Admin, stores);

        RunEnvelope run = (await (await admin.PostAsJsonAsync("/api/v1/admin/notifications/outbox/run", new RunNotificationOutboxRequest(20, false))).Content.ReadFromJsonAsync<RunEnvelope>())!;

        run.Data.SimulatedSent.Should().Be(1);
        stores.Attempts.Items[0].Provider.Should().Be(NotificationProvider.Fcm);
        stores.Attempts.Items[0].Status.Should().Be(NotificationDeliveryStatus.SimulatedSent);
        stores.FcmClient.Calls.Should().Be(1);
        stores.FcmClient.LastRequest!.RecipientToken.Should().Be("internal-fcm-token");
    }

    [Fact]
    public async Task OutboxRunWithFcmEnabledWithoutTokenFailsControlledAndRetryWorks()
    {
        var stores = new Stores(); stores.Attempts.Items.Add(new NotificationDeliveryAttempt { Id = "attempt", UserId = "rider-owner", AlertDispatchId = "alert", IncidentId = "incident", EmergencyContactId = "contact", Channel = NotificationChannel.Push, Status = NotificationDeliveryStatus.Prepared, PreparedAtUtc = Now, CreatedAtUtc = Now }); stores.Contacts.Items.Add(new EmergencyContact { Id = "contact", UserId = "rider-owner", IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Linked, LinkedUserId = "monitor-user" }); await using WebApplicationFactory<Program> factory = CreateFactory(stores, enabled: true); HttpClient admin = factory.CreateClient(); await AuthenticateAsync(admin, "fcm-admin3@example.com", UserRole.Admin, stores);

        await admin.PostAsJsonAsync("/api/v1/admin/notifications/outbox/run", new RunNotificationOutboxRequest(20, false));
        RetryFailedNotificationOutboxResponse retry = (await (await admin.PostAsJsonAsync("/api/v1/admin/notifications/outbox/retry-failed", new RetryFailedNotificationOutboxRequest(20))).Content.ReadFromJsonAsync<RetryEnvelope>())!.Data;

        stores.Attempts.Items[0].Provider.Should().Be(NotificationProvider.None);
        stores.Attempts.Items[0].Status.Should().Be(NotificationDeliveryStatus.Prepared);
        retry.Retried.Should().Be(1);
    }

    [Fact]
    public async Task ProviderStatusReportsBase64CredentialSourceWithoutExposingCredentialValues()
    {
        const string decodedJson = "{\"type\":\"service_account\"}";
        string encodedJson = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(decodedJson));
        var stores = new Stores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores, enabled: true, serviceAccountJson: null, serviceAccountJsonBase64: encodedJson); HttpClient admin = factory.CreateClient(); await AuthenticateAsync(admin, "fcm-admin4@example.com", UserRole.Admin, stores);

        HttpResponseMessage response = await admin.GetAsync("/api/v1/admin/notifications/providers/status");
        string body = await response.Content.ReadAsStringAsync();
        ProviderStatusEnvelope status = (await response.Content.ReadFromJsonAsync<ProviderStatusEnvelope>())!;

        status.Data.FcmProviderEnabled.Should().BeTrue();
        status.Data.FcmProviderConfigured.Should().BeTrue();
        status.Data.FcmCredentialSource.Should().Be("environment_json_base64");
        body.Should().NotContain(encodedJson);
        body.Should().NotContain(decodedJson);
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);
    private static async Task<User> AuthenticateAsync(HttpClient client, string email, UserRole role, Stores stores) { const string secret = "StrongPass1!"; await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, secret, secret, "Moto User", null, role == UserRole.Monitor ? "Monitor" : "Rider", true)); User user = stores.Users.Items.Single(u => u.Email == email); user.Role = role; LoginEnvelope login = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, secret))).Content.ReadFromJsonAsync<LoginEnvelope>())!; client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Data.AccessToken); return user; }
    private static WebApplicationFactory<Program> CreateFactory(Stores stores, bool enabled, string? serviceAccountJson = "{}", string? serviceAccountJsonBase64 = null) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Issuer"] = "MotoSOS", ["Jwt:Audience"] = "MotoSOS.Clients", ["Jwt:Key"] = new string('F', 48), ["Jwt:AccessTokenMinutes"] = "15", ["Jwt:RefreshTokenDays"] = "7", ["Jwt:RefreshTokenRememberMeDays"] = "30", ["MongoDb:" + "Connection" + "String"] = string.Empty, ["MongoDb:DatabaseName"] = "MotoSOS_Test", ["Notifications:Providers:Fcm:Enabled"] = enabled.ToString(), ["Notifications:Providers:Fcm:ProjectId"] = enabled ? "test-project" : null, ["Notifications:Providers:Fcm:ServiceAccountJson"] = enabled ? serviceAccountJson : null, ["Notifications:Providers:Fcm:ServiceAccountJsonBase64"] = enabled ? serviceAccountJsonBase64 : null })); builder.ConfigureTestServices(services => { services.AddSingleton<IUserRepository>(stores.Users); services.AddSingleton<IRefreshTokenRepository>(stores.RefreshTokens); services.AddSingleton<INotificationDeliveryAttemptRepository>(stores.Attempts); services.AddSingleton<IEmergencyContactRepository>(stores.Contacts); services.AddSingleton<IPushNotificationTokenRepository>(stores.Tokens); services.AddSingleton<IFcmPushClient>(stores.FcmClient); }); });
    private sealed record LoginEnvelope(bool Success, LoginResponse Data);
    private sealed record ProviderStatusEnvelope(bool Success, NotificationProviderStatusResponse Data);
    private sealed record RunEnvelope(bool Success, RunNotificationOutboxResponse Data);
    private sealed record RetryEnvelope(bool Success, RetryFailedNotificationOutboxResponse Data);
    private sealed class Stores { public Users Users { get; } = new(); public RefreshTokens RefreshTokens { get; } = new(); public Attempts Attempts { get; } = new(); public Contacts Contacts { get; } = new(); public Tokens Tokens { get; } = new(); public FcmClient FcmClient { get; } = new(); }
    private sealed class Users : IUserRepository { public List<User> Items { get; } = []; public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Id == id)); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Email == email)); public Task AddAsync(User u, CancellationToken ct) { Items.Add(u); return Task.CompletedTask; } public Task UpdateAsync(User u, CancellationToken ct) => Task.CompletedTask; }
    private sealed class RefreshTokens : IRefreshTokenRepository { public List<RefreshToken> Items { get; } = []; public Task<RefreshToken?> GetByHashAsync(string h, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.TokenHash == h)); public Task AddAsync(RefreshToken t, CancellationToken ct) { Items.Add(t); return Task.CompletedTask; } public Task UpdateAsync(RefreshToken t, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Attempts : INotificationDeliveryAttemptRepository { public List<NotificationDeliveryAttempt> Items { get; } = []; public Task<NotificationDeliveryAttempt?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.Id == id)); public Task<NotificationDeliveryAttempt?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) => Task.FromResult<NotificationDeliveryAttempt?>(null); public Task<(NotificationDeliveryAttempt Attempt, bool IsDuplicate)> AddOrGetDuplicateAsync(NotificationDeliveryAttempt attempt, CancellationToken ct) => Task.FromResult((attempt, false)); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByUserIdAsync(string userId, string? alertDispatchId, string? incidentId, NotificationDeliveryStatus? status, int pageNumber, int pageSize, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>([]); public Task<long> CountByUserIdAsync(string userId, string? alertDispatchId, string? incidentId, NotificationDeliveryStatus? status, CancellationToken ct) => Task.FromResult(0L); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByStatusAsync(NotificationDeliveryStatus status, int maxItems, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>(Items.Where(a => a.Status == status).Take(maxItems).ToArray()); public Task<NotificationDeliveryAttempt?> TryMarkSentAsync(string id, NotificationProvider provider, string? messageId, DateTimeOffset now, CancellationToken ct) { NotificationDeliveryAttempt? a = Items.FirstOrDefault(x => x.Id == id && x.Status == NotificationDeliveryStatus.Prepared); if (a is null) return Task.FromResult<NotificationDeliveryAttempt?>(null); a.Status = NotificationDeliveryStatus.SimulatedSent; a.Provider = provider; a.ProviderMessageId = messageId; a.SimulatedSentAtUtc = now; return Task.FromResult<NotificationDeliveryAttempt?>(a); } public Task<NotificationDeliveryAttempt?> TryMarkFailedAsync(string id, NotificationProvider provider, string reason, DateTimeOffset now, CancellationToken ct) { NotificationDeliveryAttempt? a = Items.FirstOrDefault(x => x.Id == id && x.Status == NotificationDeliveryStatus.Prepared); if (a is null) return Task.FromResult<NotificationDeliveryAttempt?>(null); a.Status = NotificationDeliveryStatus.Failed; a.Provider = provider; a.FailureReason = reason; a.FailedAtUtc = now; return Task.FromResult<NotificationDeliveryAttempt?>(a); } public Task<NotificationDeliveryAttempt?> TryResetFailedToPreparedAsync(string id, DateTimeOffset now, CancellationToken ct) { NotificationDeliveryAttempt? a = Items.FirstOrDefault(x => x.Id == id && x.Status == NotificationDeliveryStatus.Failed); if (a is null) return Task.FromResult<NotificationDeliveryAttempt?>(null); a.Status = NotificationDeliveryStatus.Prepared; a.Provider = NotificationProvider.None; a.FailureReason = null; a.FailedAtUtc = null; return Task.FromResult<NotificationDeliveryAttempt?>(a); } public Task<long> CountByStatusAsync(NotificationDeliveryStatus status, CancellationToken ct) => Task.FromResult((long)Items.Count(a => a.Status == status)); public Task UpdateAsync(NotificationDeliveryAttempt attempt, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Contacts : IEmergencyContactRepository { public List<EmergencyContact> Items { get; } = []; public Task<IReadOnlyList<EmergencyContact>> GetActiveByUserIdAsync(string userId, CancellationToken ct) => Task.FromResult<IReadOnlyList<EmergencyContact>>([]); public Task<EmergencyContact?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(c => c.Id == id)); public Task<EmergencyContact?> GetByLinkingCodeAsync(string linkingCode, CancellationToken ct) => Task.FromResult<EmergencyContact?>(null); public Task<int> CountActiveByUserIdAsync(string userId, CancellationToken ct) => Task.FromResult(0); public Task AddAsync(EmergencyContact contact, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(EmergencyContact contact, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Tokens : IPushNotificationTokenRepository { public List<PushNotificationToken> Items { get; } = []; public Task<PushNotificationToken?> GetLatestActiveFcmByUserIdAsync(string userId, CancellationToken ct) => Task.FromResult(Items.Where(t => t.UserId == userId && t.Status == PushNotificationTokenStatus.Active && t.Channel == PushTokenChannel.Fcm && t.Platform is PushTokenPlatform.Android or PushTokenPlatform.Web).OrderByDescending(t => t.LastSeenAtUtc).FirstOrDefault()); public Task<PushNotificationToken?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult<PushNotificationToken?>(null); public Task<PushNotificationToken?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) => Task.FromResult<PushNotificationToken?>(null); public Task<(PushNotificationToken Token, bool IsDuplicate)> AddOrGetDuplicateAsync(PushNotificationToken token, CancellationToken ct) => Task.FromResult((token, false)); public Task UpdateAsync(PushNotificationToken token, CancellationToken ct) => Task.CompletedTask; public Task<long> RevokeActiveTokensForScopeAsync(string userId, PushTokenPlatform platform, PushTokenChannel channel, string? deviceId, DateTimeOffset now, CancellationToken ct) => Task.FromResult(0L); public Task<IReadOnlyList<PushNotificationToken>> ListByUserIdAsync(string userId, PushNotificationTokenQuery query, CancellationToken ct) => Task.FromResult<IReadOnlyList<PushNotificationToken>>([]); public Task<long> CountByUserIdAsync(string userId, PushNotificationTokenQuery query, CancellationToken ct) => Task.FromResult(0L); public Task<PushNotificationTokenStatusSummary> GetStatusByUserIdAsync(string userId, CancellationToken ct) => Task.FromResult(new PushNotificationTokenStatusSummary(0, 0, false, false, false, false, null)); public Task<IReadOnlyList<PushNotificationToken>> ListAsync(PushNotificationTokenQuery query, CancellationToken ct) => Task.FromResult<IReadOnlyList<PushNotificationToken>>([]); public Task<long> CountAsync(PushNotificationTokenQuery query, CancellationToken ct) => Task.FromResult(0L); }
    private sealed class FcmClient : IFcmPushClient { public int Calls { get; private set; } public FcmPushRequest? LastRequest { get; private set; } public Task<FcmPushResult> SendAsync(FcmPushRequest request, CancellationToken ct) { Calls++; LastRequest = request; return Task.FromResult(new FcmPushResult(true, "fcm-message", null, null)); } }
}
