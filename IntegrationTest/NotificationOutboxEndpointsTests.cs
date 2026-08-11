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
using MotoSOS.API.Modules.NotificationOutbox.Contracts;
using MotoSOS.API.Modules.Notifications.Application;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace IntegrationTest;

public sealed class NotificationOutboxEndpointsTests
{
    [Fact]
    public async Task OutboxEndpointsRequireAuthentication()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory(new Stores()); HttpClient client = factory.CreateClient();
        (await client.PostAsJsonAsync("/api/v1/admin/notifications/outbox/run", new RunNotificationOutboxRequest(null, null))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/admin/notifications/outbox/status")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/admin/notifications/outbox/worker/status")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/v1/admin/notifications/outbox/retry-failed", new RetryFailedNotificationOutboxRequest(null))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RiderAndMonitorAreForbiddenAndAdminCanRun()
    {
        var stores = new Stores(); stores.Attempts.Items.Add(Attempt(NotificationDeliveryStatus.Prepared)); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient admin = factory.CreateClient(); HttpClient rider = factory.CreateClient(); HttpClient monitor = factory.CreateClient(); await AuthenticateAsync(admin, "outbox-admin@example.com", UserRole.Admin, stores); await AuthenticateAsync(rider, "outbox-rider@example.com", UserRole.Rider, stores); await AuthenticateAsync(monitor, "outbox-monitor@example.com", UserRole.Monitor, stores);

        (await admin.PostAsJsonAsync("/api/v1/admin/notifications/outbox/run", new RunNotificationOutboxRequest(20, false))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await rider.PostAsJsonAsync("/api/v1/admin/notifications/outbox/run", new RunNotificationOutboxRequest(20, false))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await monitor.GetAsync("/api/v1/admin/notifications/outbox/status")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await rider.GetAsync("/api/v1/admin/notifications/outbox/worker/status")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await monitor.GetAsync("/api/v1/admin/notifications/outbox/worker/status")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        string workerStatus = await (await admin.GetAsync("/api/v1/admin/notifications/outbox/worker/status")).Content.ReadAsStringAsync();
        workerStatus.Should().Contain("enabled").And.Contain("intervalSeconds").And.Contain("maxItemsPerRun").And.Contain("simulateFailures").And.Contain("runOnStartup").And.Contain("lastProcessedCount").And.Contain("lastFailedCount").And.Contain("lastError").And.NotContain("pass" + "word" + "Hash").And.NotContain("refresh" + "Token").And.NotContain("access" + "Token").And.NotContain("token").And.NotContain("payload");
    }

    [Fact]
    public async Task RunStatusAndRetryFollowOutboxRules()
    {
        var stores = new Stores(); NotificationDeliveryAttempt prepared = Attempt(NotificationDeliveryStatus.Prepared); NotificationDeliveryAttempt cancelled = Attempt(NotificationDeliveryStatus.Cancelled); NotificationDeliveryAttempt sent = Attempt(NotificationDeliveryStatus.SimulatedSent); NotificationDeliveryAttempt failed = Attempt(NotificationDeliveryStatus.Failed); stores.Attempts.Items.AddRange([prepared, cancelled, sent, failed]); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient admin = factory.CreateClient(); await AuthenticateAsync(admin, "outbox-admin2@example.com", UserRole.Admin, stores);

        string run = await (await admin.PostAsJsonAsync("/api/v1/admin/notifications/outbox/run", new RunNotificationOutboxRequest(20, false))).Content.ReadAsStringAsync();
        HttpResponseMessage invalid = await admin.PostAsJsonAsync("/api/v1/admin/notifications/outbox/run", new RunNotificationOutboxRequest(0, false));
        HttpResponseMessage retry = await admin.PostAsJsonAsync("/api/v1/admin/notifications/outbox/retry-failed", new RetryFailedNotificationOutboxRequest(20));
        string status = await (await admin.GetAsync("/api/v1/admin/notifications/outbox/status")).Content.ReadAsStringAsync();

        run.Should().Contain("simulatedSent").And.NotContain("email").And.NotContain("phone").And.NotContain("password" + "Hash").And.NotContain("refresh" + "Token").And.NotContain("access" + "Token").And.NotContain("device" + "Identifier");
        prepared.Status.Should().Be(NotificationDeliveryStatus.SimulatedSent);
        cancelled.Status.Should().Be(NotificationDeliveryStatus.Cancelled);
        sent.Status.Should().Be(NotificationDeliveryStatus.SimulatedSent);
        retry.StatusCode.Should().Be(HttpStatusCode.OK);
        failed.Status.Should().Be(NotificationDeliveryStatus.Prepared);
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        status.Should().Contain("prepared").And.Contain("simulatedSent").And.Contain("failed").And.Contain("cancelled");
    }

    [Fact]
    public async Task SimulateFailuresMarksPreparedFailedWithControlledReason()
    {
        var stores = new Stores(); NotificationDeliveryAttempt prepared = Attempt(NotificationDeliveryStatus.Prepared); stores.Attempts.Items.Add(prepared); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient admin = factory.CreateClient(); await AuthenticateAsync(admin, "outbox-admin3@example.com", UserRole.Admin, stores);

        string body = await (await admin.PostAsJsonAsync("/api/v1/admin/notifications/outbox/run", new RunNotificationOutboxRequest(20, true))).Content.ReadAsStringAsync();

        prepared.Status.Should().Be(NotificationDeliveryStatus.Failed);
        prepared.FailureReason.Should().Be("simulated_failure_requested");
        body.Should().Contain("simulated_failure_requested");
    }

    [Fact]
    public async Task WorkerStatusReflectsConfiguredSafeOptions()
    {
        var stores = new Stores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores, new Dictionary<string, string?> { ["Notifications:OutboxWorker:Enabled"] = "true", ["Notifications:OutboxWorker:IntervalSeconds"] = "30", ["Notifications:OutboxWorker:MaxItemsPerRun"] = "20", ["Notifications:OutboxWorker:SimulateFailures"] = "false", ["Notifications:OutboxWorker:RunOnStartup"] = "false" }); HttpClient admin = factory.CreateClient(); await AuthenticateAsync(admin, "outbox-admin4@example.com", UserRole.Admin, stores);

        NotificationOutboxWorkerStatusEnvelope response = (await (await admin.GetAsync("/api/v1/admin/notifications/outbox/worker/status")).Content.ReadFromJsonAsync<NotificationOutboxWorkerStatusEnvelope>())!;

        response.Data.Enabled.Should().BeTrue();
        response.Data.IntervalSeconds.Should().Be(30);
        response.Data.MaxItemsPerRun.Should().Be(20);
        response.Data.SimulateFailures.Should().BeFalse();
        response.Data.RunOnStartup.Should().BeFalse();
        response.Data.LastError.Should().BeNull();
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);
    private static NotificationDeliveryAttempt Attempt(NotificationDeliveryStatus status) => new() { Id = Guid.NewGuid().ToString("N"), Status = status, Channel = NotificationChannel.Sms, Provider = NotificationProvider.None, PreparedAtUtc = Now, CreatedAtUtc = Now };
    private static async Task<User> AuthenticateAsync(HttpClient client, string email, UserRole role, Stores stores) { var register = new RegisterRequest(email, "StrongPass1!", "StrongPass1!", "Moto Rider", "+52 555", "Rider", true); await client.PostAsJsonAsync("/api/v1/auth/register", register); User user = stores.Users.Items.Single(u => u.Email == email); user.Role = role; LoginEnvelope login = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, register.Password))).Content.ReadFromJsonAsync<LoginEnvelope>())!; client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Data.AccessToken); return user; }
    private static WebApplicationFactory<Program> CreateFactory(Stores stores, IReadOnlyDictionary<string, string?>? workerOptions = null) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); builder.ConfigureAppConfiguration((_, c) => { var values = new Dictionary<string, string?> { ["Jwt:Issuer"] = "MotoSOS", ["Jwt:Audience"] = "MotoSOS.Clients", ["Jwt:Key"] = new string('O', 48), ["Jwt:AccessTokenMinutes"] = "15", ["Jwt:RefreshTokenDays"] = "7", ["Jwt:RefreshTokenRememberMeDays"] = "30", ["MongoDb:" + "Connection" + "String"] = string.Empty, ["MongoDb:DatabaseName"] = "MotoSOS_Test" }; if (workerOptions is not null) foreach (KeyValuePair<string, string?> item in workerOptions) values[item.Key] = item.Value; c.AddInMemoryCollection(values); }); builder.ConfigureTestServices(services => { services.AddSingleton<IUserRepository>(stores.Users); services.AddSingleton<IRefreshTokenRepository>(stores.RefreshTokens); services.AddSingleton<INotificationDeliveryAttemptRepository>(stores.Attempts); }); });
    private sealed record LoginEnvelope(bool Success, LoginResponse Data);
    private sealed record NotificationOutboxWorkerStatusEnvelope(bool Success, NotificationOutboxWorkerStatusResponse Data);
    private sealed class Stores { public Users Users { get; } = new(); public RefreshTokens RefreshTokens { get; } = new(); public Attempts Attempts { get; } = new(); }
    private sealed class Users : IUserRepository { public List<User> Items { get; } = []; public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Id == id)); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Email == email)); public Task AddAsync(User u, CancellationToken ct) { Items.Add(u); return Task.CompletedTask; } public Task UpdateAsync(User u, CancellationToken ct) => Task.CompletedTask; }
    private sealed class RefreshTokens : IRefreshTokenRepository { public List<RefreshToken> Items { get; } = []; public Task<RefreshToken?> GetByHashAsync(string h, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.TokenHash == h)); public Task AddAsync(RefreshToken t, CancellationToken ct) { Items.Add(t); return Task.CompletedTask; } public Task UpdateAsync(RefreshToken t, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Attempts : INotificationDeliveryAttemptRepository
    {
        public List<NotificationDeliveryAttempt> Items { get; } = [];
        public Task<NotificationDeliveryAttempt?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.Id == id)); public Task<NotificationDeliveryAttempt?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) => Task.FromResult<NotificationDeliveryAttempt?>(null); public Task<(NotificationDeliveryAttempt Attempt, bool IsDuplicate)> AddOrGetDuplicateAsync(NotificationDeliveryAttempt attempt, CancellationToken ct) => Task.FromResult((attempt, false)); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByUserIdAsync(string userId, string? alertDispatchId, string? incidentId, NotificationDeliveryStatus? status, int pageNumber, int pageSize, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>([]); public Task<long> CountByUserIdAsync(string userId, string? alertDispatchId, string? incidentId, NotificationDeliveryStatus? status, CancellationToken ct) => Task.FromResult(0L); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByStatusAsync(NotificationDeliveryStatus status, int maxItems, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>(Items.Where(a => a.Status == status).Take(maxItems).ToArray()); public Task<NotificationDeliveryAttempt?> TryMarkSimulatedSentAsync(string attemptId, DateTimeOffset now, CancellationToken ct) { NotificationDeliveryAttempt? a = Items.FirstOrDefault(x => x.Id == attemptId && x.Status == NotificationDeliveryStatus.Prepared); if (a is null) return Task.FromResult<NotificationDeliveryAttempt?>(null); a.Status = NotificationDeliveryStatus.SimulatedSent; a.Provider = NotificationProvider.Simulated; a.SimulatedSentAtUtc = now; a.UpdatedAtUtc = now; a.LastStatusChangedAtUtc = now; return Task.FromResult<NotificationDeliveryAttempt?>(a); }
        public Task<NotificationDeliveryAttempt?> TryMarkFailedAsync(string attemptId, string failureReason, DateTimeOffset now, CancellationToken ct) { NotificationDeliveryAttempt? a = Items.FirstOrDefault(x => x.Id == attemptId && x.Status == NotificationDeliveryStatus.Prepared); if (a is null) return Task.FromResult<NotificationDeliveryAttempt?>(null); a.Status = NotificationDeliveryStatus.Failed; a.Provider = NotificationProvider.Simulated; a.ProviderMessageId = null; a.FailureReason = failureReason; a.FailedAtUtc = now; a.UpdatedAtUtc = now; a.LastStatusChangedAtUtc = now; return Task.FromResult<NotificationDeliveryAttempt?>(a); }
        public Task<NotificationDeliveryAttempt?> TryResetFailedToPreparedAsync(string attemptId, DateTimeOffset now, CancellationToken ct) { NotificationDeliveryAttempt? a = Items.FirstOrDefault(x => x.Id == attemptId && x.Status == NotificationDeliveryStatus.Failed); if (a is null) return Task.FromResult<NotificationDeliveryAttempt?>(null); a.Status = NotificationDeliveryStatus.Prepared; a.Provider = NotificationProvider.None; a.FailureReason = null; a.FailedAtUtc = null; a.UpdatedAtUtc = now; a.LastStatusChangedAtUtc = now; return Task.FromResult<NotificationDeliveryAttempt?>(a); }
        public Task<long> CountByStatusAsync(NotificationDeliveryStatus status, CancellationToken ct) => Task.FromResult((long)Items.Count(a => a.Status == status)); public Task UpdateAsync(NotificationDeliveryAttempt attempt, CancellationToken ct) => Task.CompletedTask;
    }
}
