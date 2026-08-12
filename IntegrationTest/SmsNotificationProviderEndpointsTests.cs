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
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.Notifications.Providers;
using MotoSOS.API.Modules.PushNotificationTokens.Application;
using MotoSOS.API.Modules.PushNotificationTokens.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace IntegrationTest;

public sealed class SmsNotificationProviderEndpointsTests
{
    [Fact]
    public async Task OutboxRunWithSmsEnabledMarksAttemptSmsSent()
    {
        var stores = new Stores();
        stores.Attempts.Items.Add(Attempt("55 1234 5678"));
        await using WebApplicationFactory<Program> factory = CreateFactory(stores, smsEnabled: true);
        HttpClient admin = factory.CreateClient(); await AuthenticateAsync(admin, "sms-admin@example.com", UserRole.Admin, stores);

        RunEnvelope run = (await (await admin.PostAsJsonAsync("/api/v1/admin/notifications/outbox/run", new RunNotificationOutboxRequest(20, false))).Content.ReadFromJsonAsync<RunEnvelope>())!;

        run.Data.SimulatedSent.Should().Be(1);
        stores.Attempts.Items[0].Provider.Should().Be(NotificationProvider.Sms);
        stores.Attempts.Items[0].Status.Should().Be(NotificationDeliveryStatus.SimulatedSent);
        stores.SmsSender.Calls.Should().Be(1);
        stores.SmsSender.LastMessage!.ToPhoneNumber.Should().Be("+525512345678");
        stores.SmsSender.LastMessage.Body.Should().Contain("ID: sms-attempt").And.NotContain("token");
    }

    [Fact]
    public async Task OutboxRunWithSmsFailureMarksAttemptSmsFailed()
    {
        var stores = new Stores(); stores.SmsSender.Fail = true;
        stores.Attempts.Items.Add(Attempt("+525512345678"));
        await using WebApplicationFactory<Program> factory = CreateFactory(stores, smsEnabled: true);
        HttpClient admin = factory.CreateClient(); await AuthenticateAsync(admin, "sms-admin2@example.com", UserRole.Admin, stores);

        RunEnvelope run = (await (await admin.PostAsJsonAsync("/api/v1/admin/notifications/outbox/run", new RunNotificationOutboxRequest(20, false))).Content.ReadFromJsonAsync<RunEnvelope>())!;

        run.Data.Failed.Should().Be(1);
        stores.Attempts.Items[0].Provider.Should().Be(NotificationProvider.Sms);
        stores.Attempts.Items[0].Status.Should().Be(NotificationDeliveryStatus.Failed);
        stores.Attempts.Items[0].FailureReason.Should().Be("sms_provider_failed");
    }

    [Fact]
    public async Task OutboxRunWithSmsDisabledKeepsSimulatedProvider()
    {
        var stores = new Stores();
        stores.Attempts.Items.Add(Attempt("55 1234 5678"));
        await using WebApplicationFactory<Program> factory = CreateFactory(stores, smsEnabled: false);
        HttpClient admin = factory.CreateClient(); await AuthenticateAsync(admin, "sms-admin3@example.com", UserRole.Admin, stores);

        await admin.PostAsJsonAsync("/api/v1/admin/notifications/outbox/run", new RunNotificationOutboxRequest(20, false));

        stores.Attempts.Items[0].Provider.Should().Be(NotificationProvider.Simulated);
        stores.Attempts.Items[0].Status.Should().Be(NotificationDeliveryStatus.SimulatedSent);
        stores.SmsSender.Calls.Should().Be(0);
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 12, 12, 0, 0, TimeSpan.Zero);
    private static NotificationDeliveryAttempt Attempt(string phone) => new() { Id = "sms-attempt", UserId = "rider-owner", AlertDispatchId = "alert", IncidentId = "incident", EmergencyContactId = "contact", ContactPhoneNumber = phone, Channel = NotificationChannel.Sms, Status = NotificationDeliveryStatus.Prepared, PreparedAtUtc = Now, CreatedAtUtc = Now };
    private static async Task<User> AuthenticateAsync(HttpClient client, string email, UserRole role, Stores stores) { const string secret = "StrongPass1!"; await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, secret, secret, "Moto User", null, role == UserRole.Monitor ? "Monitor" : "Rider", true)); User user = stores.Users.Items.Single(u => u.Email == email); user.Role = role; LoginEnvelope login = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, secret))).Content.ReadFromJsonAsync<LoginEnvelope>())!; client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Data.AccessToken); return user; }
    private static WebApplicationFactory<Program> CreateFactory(Stores stores, bool smsEnabled) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Issuer"] = "MotoSOS", ["Jwt:Audience"] = "MotoSOS.Clients", ["Jwt:Key"] = new string('S', 48), ["Jwt:AccessTokenMinutes"] = "15", ["Jwt:RefreshTokenDays"] = "7", ["Jwt:RefreshTokenRememberMeDays"] = "30", ["MongoDb:" + "Connection" + "String"] = string.Empty, ["MongoDb:DatabaseName"] = "MotoSOS_Test", ["Notifications:Providers:Sms:Enabled"] = smsEnabled.ToString(), ["Notifications:Providers:Sms:Provider"] = smsEnabled ? "Brevo" : null, ["Notifications:Providers:Sms:ApiKey"] = smsEnabled ? "sms-api-key" : null, ["Notifications:Providers:Sms:Sender"] = smsEnabled ? "MotoSOS" : null, ["Notifications:Providers:Sms:DefaultCountryCode"] = smsEnabled ? "+52" : null, ["Notifications:Providers:Sms:TimeoutSeconds"] = smsEnabled ? "15" : null })); builder.ConfigureTestServices(services => { services.AddSingleton<IUserRepository>(stores.Users); services.AddSingleton<IRefreshTokenRepository>(stores.RefreshTokens); services.AddSingleton<INotificationDeliveryAttemptRepository>(stores.Attempts); services.AddSingleton<IEmergencyContactRepository>(stores.Contacts); services.AddSingleton<IPushNotificationTokenRepository>(stores.Tokens); services.AddSingleton<ISmsNotificationSender>(stores.SmsSender); }); });
    private sealed record LoginEnvelope(bool Success, LoginResponse Data);
    private sealed record RunEnvelope(bool Success, RunNotificationOutboxResponse Data);
    private sealed class Stores { public Users Users { get; } = new(); public RefreshTokens RefreshTokens { get; } = new(); public Attempts Attempts { get; } = new(); public Contacts Contacts { get; } = new(); public Tokens Tokens { get; } = new(); public SmsSender SmsSender { get; } = new(); }
    private sealed class Users : IUserRepository { public List<User> Items { get; } = []; public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Id == id)); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Email == email)); public Task AddAsync(User u, CancellationToken ct) { Items.Add(u); return Task.CompletedTask; } public Task UpdateAsync(User u, CancellationToken ct) => Task.CompletedTask; }
    private sealed class RefreshTokens : IRefreshTokenRepository { public List<RefreshToken> Items { get; } = []; public Task<RefreshToken?> GetByHashAsync(string h, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.TokenHash == h)); public Task AddAsync(RefreshToken t, CancellationToken ct) { Items.Add(t); return Task.CompletedTask; } public Task UpdateAsync(RefreshToken t, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Attempts : INotificationDeliveryAttemptRepository { public List<NotificationDeliveryAttempt> Items { get; } = []; public Task<NotificationDeliveryAttempt?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.Id == id)); public Task<NotificationDeliveryAttempt?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) => Task.FromResult<NotificationDeliveryAttempt?>(null); public Task<(NotificationDeliveryAttempt Attempt, bool IsDuplicate)> AddOrGetDuplicateAsync(NotificationDeliveryAttempt attempt, CancellationToken ct) => Task.FromResult((attempt, false)); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByUserIdAsync(string userId, string? alertDispatchId, string? incidentId, NotificationDeliveryStatus? status, int pageNumber, int pageSize, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>([]); public Task<long> CountByUserIdAsync(string userId, string? alertDispatchId, string? incidentId, NotificationDeliveryStatus? status, CancellationToken ct) => Task.FromResult(0L); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByStatusAsync(NotificationDeliveryStatus status, int maxItems, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>(Items.Where(a => a.Status == status).Take(maxItems).ToArray()); public Task<NotificationDeliveryAttempt?> TryMarkSentAsync(string id, NotificationProvider provider, string? messageId, DateTimeOffset now, CancellationToken ct) { NotificationDeliveryAttempt? a = Items.FirstOrDefault(x => x.Id == id && x.Status == NotificationDeliveryStatus.Prepared); if (a is null) return Task.FromResult<NotificationDeliveryAttempt?>(null); a.Status = NotificationDeliveryStatus.SimulatedSent; a.Provider = provider; a.ProviderMessageId = messageId; a.SimulatedSentAtUtc = now; return Task.FromResult<NotificationDeliveryAttempt?>(a); } public Task<NotificationDeliveryAttempt?> TryMarkFailedAsync(string id, NotificationProvider provider, string reason, DateTimeOffset now, CancellationToken ct) { NotificationDeliveryAttempt? a = Items.FirstOrDefault(x => x.Id == id && x.Status == NotificationDeliveryStatus.Prepared); if (a is null) return Task.FromResult<NotificationDeliveryAttempt?>(null); a.Status = NotificationDeliveryStatus.Failed; a.Provider = provider; a.FailureReason = reason; a.FailedAtUtc = now; return Task.FromResult<NotificationDeliveryAttempt?>(a); } public Task<NotificationDeliveryAttempt?> TryMarkSimulatedSentAsync(string id, DateTimeOffset now, CancellationToken ct) => TryMarkSentAsync(id, NotificationProvider.Simulated, null, now, ct); public Task<NotificationDeliveryAttempt?> TryMarkSimulatedSentAsync(string id, string? messageId, DateTimeOffset now, CancellationToken ct) => TryMarkSentAsync(id, NotificationProvider.Simulated, messageId, now, ct); public Task<NotificationDeliveryAttempt?> TryMarkFailedAsync(string id, string reason, DateTimeOffset now, CancellationToken ct) => TryMarkFailedAsync(id, NotificationProvider.Simulated, reason, now, ct); public Task<NotificationDeliveryAttempt?> TryResetFailedToPreparedAsync(string id, DateTimeOffset now, CancellationToken ct) => Task.FromResult<NotificationDeliveryAttempt?>(null); public Task<long> CountByStatusAsync(NotificationDeliveryStatus status, CancellationToken ct) => Task.FromResult((long)Items.Count(a => a.Status == status)); public Task UpdateAsync(NotificationDeliveryAttempt attempt, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Contacts : IEmergencyContactRepository { public Task<IReadOnlyList<EmergencyContact>> GetActiveByUserIdAsync(string userId, CancellationToken ct) => Task.FromResult<IReadOnlyList<EmergencyContact>>([]); public Task<EmergencyContact?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult<EmergencyContact?>(null); public Task<EmergencyContact?> GetByLinkingCodeAsync(string linkingCode, CancellationToken ct) => Task.FromResult<EmergencyContact?>(null); public Task<int> CountActiveByUserIdAsync(string userId, CancellationToken ct) => Task.FromResult(0); public Task AddAsync(EmergencyContact contact, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(EmergencyContact contact, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Tokens : IPushNotificationTokenRepository { public Task<PushNotificationToken?> GetLatestActiveFcmByUserIdAsync(string userId, CancellationToken ct) => Task.FromResult<PushNotificationToken?>(null); public Task<PushNotificationToken?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult<PushNotificationToken?>(null); public Task<PushNotificationToken?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) => Task.FromResult<PushNotificationToken?>(null); public Task<(PushNotificationToken Token, bool IsDuplicate)> AddOrGetDuplicateAsync(PushNotificationToken token, CancellationToken ct) => Task.FromResult((token, false)); public Task UpdateAsync(PushNotificationToken token, CancellationToken ct) => Task.CompletedTask; public Task<long> RevokeActiveTokensForScopeAsync(string userId, PushTokenPlatform platform, PushTokenChannel channel, string? deviceId, DateTimeOffset now, CancellationToken ct) => Task.FromResult(0L); public Task<IReadOnlyList<PushNotificationToken>> ListByUserIdAsync(string userId, PushNotificationTokenQuery query, CancellationToken ct) => Task.FromResult<IReadOnlyList<PushNotificationToken>>([]); public Task<long> CountByUserIdAsync(string userId, PushNotificationTokenQuery query, CancellationToken ct) => Task.FromResult(0L); public Task<PushNotificationTokenStatusSummary> GetStatusByUserIdAsync(string userId, CancellationToken ct) => Task.FromResult(new PushNotificationTokenStatusSummary(0, 0, false, false, false, false, null)); public Task<IReadOnlyList<PushNotificationToken>> ListAsync(PushNotificationTokenQuery query, CancellationToken ct) => Task.FromResult<IReadOnlyList<PushNotificationToken>>([]); public Task<long> CountAsync(PushNotificationTokenQuery query, CancellationToken ct) => Task.FromResult(0L); }
    private sealed class SmsSender : ISmsNotificationSender { public int Calls { get; private set; } public SmsNotificationMessage? LastMessage { get; private set; } public bool Fail { get; set; } public Task<string?> SendAsync(SmsNotificationMessage message, SmsNotificationProviderOptions options, CancellationToken cancellationToken) { Calls++; LastMessage = message; if (Fail) throw new InvalidOperationException("sms-api-key must not leak"); return Task.FromResult<string?>("sms-message"); } }
}
