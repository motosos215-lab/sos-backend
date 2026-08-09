using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MotoSOS.API.Modules.AlertAcknowledgements.Application;
using MotoSOS.API.Modules.AlertAcknowledgements.Domain;
using MotoSOS.API.Modules.AlertDispatch.Application;
using MotoSOS.API.Modules.AlertDispatch.Domain;
using MotoSOS.API.Modules.Auth.Application;
using MotoSOS.API.Modules.Auth.Contracts;
using MotoSOS.API.Modules.Auth.Domain;
using MotoSOS.API.Modules.Escalations.Application;
using MotoSOS.API.Modules.Escalations.Contracts;
using MotoSOS.API.Modules.Escalations.Domain;
using MotoSOS.API.Modules.Incidents.Application;
using MotoSOS.API.Modules.Incidents.Domain;
using MotoSOS.API.Modules.Notifications.Application;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace IntegrationTest;

public sealed class AutomaticEscalationWorkerEndpointsTests
{
    [Fact]
    public async Task StatusAndRunRequireAuthentication()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory(new Stores());
        HttpClient client = factory.CreateClient();

        (await client.GetAsync("/api/v1/admin/escalations/worker/status")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/v1/admin/escalations/worker/run", new RunAutomaticEscalationRequest(null, null))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RiderMonitorForbiddenAndAdminCanReadStatus()
    {
        var stores = new Stores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient admin = factory.CreateClient(); HttpClient rider = factory.CreateClient(); HttpClient monitor = factory.CreateClient(); await AuthenticateAsync(admin, "automatic-escalation-admin@example.com", UserRole.Admin, stores); await AuthenticateAsync(rider, "automatic-escalation-rider@example.com", UserRole.Rider, stores); await AuthenticateAsync(monitor, "automatic-escalation-monitor@example.com", UserRole.Monitor, stores);

        (await rider.GetAsync("/api/v1/admin/escalations/worker/status")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await monitor.PostAsJsonAsync("/api/v1/admin/escalations/worker/run", new RunAutomaticEscalationRequest(null, null))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        string body = await (await admin.GetAsync("/api/v1/admin/escalations/worker/status")).Content.ReadAsStringAsync();

        body.Should().Contain("isEnabled").And.Contain("isRunning").And.NotContain("pass" + "word").And.NotContain("refresh" + "Token").And.NotContain("access" + "Token").And.NotContain("stackTrace");
    }

    [Fact]
    public async Task AdminManualRunCreatesAutomaticEscalationAndIsIdempotent()
    {
        var stores = new Stores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient admin = factory.CreateClient(); HttpClient rider = factory.CreateClient(); await AuthenticateAsync(admin, "automatic-escalation-admin2@example.com", UserRole.Admin, stores); User riderUser = await AuthenticateAsync(rider, "automatic-escalation-rider2@example.com", UserRole.Rider, stores); SeedCandidate(stores, riderUser.Id);

        HttpResponseMessage first = await admin.PostAsJsonAsync("/api/v1/admin/escalations/worker/run", new RunAutomaticEscalationRequest(20, 300));
        HttpResponseMessage second = await admin.PostAsJsonAsync("/api/v1/admin/escalations/worker/run", new RunAutomaticEscalationRequest(20, 300));
        HttpResponseMessage invalid = await admin.PostAsJsonAsync("/api/v1/admin/escalations/worker/run", new RunAutomaticEscalationRequest(101, 30));

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        stores.Escalations.Items.Should().ContainSingle();
        stores.Escalations.Items.Single().Reason.Should().Be(EmergencyEscalationReason.NoAcknowledgement);
        stores.Escalations.Items.Single().Level.Should().Be(EmergencyEscalationLevel.Level1);
        stores.Incidents.Items.Single().Status.Should().Be(IncidentStatus.Open);
        stores.Alerts.Items.Single().Status.Should().Be(AlertDispatchStatus.PendingDispatch);
        stores.Attempts.Items.Should().ContainSingle();
        stores.Acks.Items.Should().BeEmpty();
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);
    private static void SeedCandidate(Stores stores, string riderId)
    {
        stores.Incidents.Items.Add(new Incident { Id = "incident", UserId = riderId, TripId = "trip", Status = IncidentStatus.Open, CreatedAtUtc = Now.AddMinutes(-20) });
        stores.Alerts.Items.Add(new AlertDispatchRequest { Id = "dispatch", UserId = riderId, IncidentId = "incident", TripId = "trip", Status = AlertDispatchStatus.PendingDispatch, RequestedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-20), CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-20), IdempotencyKey = Guid.NewGuid().ToString() });
        stores.Attempts.Items.Add(new NotificationDeliveryAttempt { Id = "attempt", UserId = riderId, AlertDispatchId = "dispatch", IncidentId = "incident", TripId = "trip", EmergencyContactId = "contact", Status = NotificationDeliveryStatus.SimulatedSent, CreatedAtUtc = Now.AddMinutes(-20), SimulatedSentAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10), IdempotencyKey = Guid.NewGuid().ToString() });
    }

    private static async Task<User> AuthenticateAsync(HttpClient client, string email, UserRole role, Stores stores) { var register = new RegisterRequest(email, "StrongPass1!", "StrongPass1!", "Moto Rider", "+52 555", "Rider", true); await client.PostAsJsonAsync("/api/v1/auth/register", register); User user = stores.Users.Items.Single(u => u.Email == email); user.Role = role; LoginEnvelope login = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, register.Password))).Content.ReadFromJsonAsync<LoginEnvelope>())!; client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Data.AccessToken); return user; }
    private static WebApplicationFactory<Program> CreateFactory(Stores stores) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Issuer"] = "MotoSOS", ["Jwt:Audience"] = "MotoSOS.Clients", ["Jwt:Key"] = new string('E', 48), ["Jwt:AccessTokenMinutes"] = "15", ["Jwt:RefreshTokenDays"] = "7", ["Jwt:RefreshTokenRememberMeDays"] = "30", ["MongoDb:" + "Connection" + "String"] = string.Empty, ["MongoDb:DatabaseName"] = "MotoSOS_Test" })); builder.ConfigureTestServices(services => { services.AddSingleton<IUserRepository>(stores.Users); services.AddSingleton<IRefreshTokenRepository>(stores.RefreshTokens); services.AddSingleton<IAlertDispatchRepository>(stores.Alerts); services.AddSingleton<IIncidentRepository>(stores.Incidents); services.AddSingleton<INotificationDeliveryAttemptRepository>(stores.Attempts); services.AddSingleton<IAlertAcknowledgementRepository>(stores.Acks); services.AddSingleton<IEmergencyEscalationRepository>(stores.Escalations); }); });
    private sealed record LoginEnvelope(bool Success, LoginResponse Data);
    private sealed class Stores { public Users Users { get; } = new(); public RefreshTokens RefreshTokens { get; } = new(); public Alerts Alerts { get; } = new(); public Incidents Incidents { get; } = new(); public Attempts Attempts { get; } = new(); public Acks Acks { get; } = new(); public Escalations Escalations { get; } = new(); }
    private sealed class Users : IUserRepository { public List<User> Items { get; } = []; public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Id == id)); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Email == email)); public Task AddAsync(User u, CancellationToken ct) { Items.Add(u); return Task.CompletedTask; } public Task UpdateAsync(User u, CancellationToken ct) => Task.CompletedTask; }
    private sealed class RefreshTokens : IRefreshTokenRepository { public List<RefreshToken> Items { get; } = []; public Task<RefreshToken?> GetByHashAsync(string h, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.TokenHash == h)); public Task AddAsync(RefreshToken t, CancellationToken ct) { Items.Add(t); return Task.CompletedTask; } public Task UpdateAsync(RefreshToken t, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Alerts : IAlertDispatchRepository { public List<AlertDispatchRequest> Items { get; } = []; public Task<AlertDispatchRequest?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id)); public Task<AlertDispatchRequest?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult<AlertDispatchRequest?>(null); public Task<(AlertDispatchRequest AlertDispatch, bool IsDuplicate)> AddOrGetDuplicateAsync(AlertDispatchRequest a, CancellationToken ct) => Task.FromResult((a, false)); public Task<IReadOnlyList<AlertDispatchRequest>> ListCandidatesForAutomaticEscalationAsync(DateTimeOffset cutoffUtc, int maxItems, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertDispatchRequest>>(Items.Where(i => i.Status == AlertDispatchStatus.PendingDispatch && i.RequestedAtUtc <= cutoffUtc).Take(maxItems).ToArray()); public Task<IReadOnlyList<AlertDispatchRequest>> ListByUserIdAsync(string u, AlertDispatchStatus? s, string? i, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertDispatchRequest>>([]); public Task<long> CountByUserIdAsync(string u, AlertDispatchStatus? s, string? i, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(AlertDispatchRequest a, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Incidents : IIncidentRepository { public List<Incident> Items { get; } = []; public Task<Incident?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id)); public Task<Incident?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult<Incident?>(null); public Task<(Incident Incident, bool IsDuplicate)> AddOrGetDuplicateAsync(Incident i, CancellationToken ct) => Task.FromResult((i, false)); public Task<IReadOnlyList<Incident>> ListByUserIdAsync(string u, IncidentStatus? s, string? t, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<Incident>>([]); public Task<long> CountByUserIdAsync(string u, IncidentStatus? s, string? t, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(Incident i, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Attempts : INotificationDeliveryAttemptRepository { public List<NotificationDeliveryAttempt> Items { get; } = []; public Task<NotificationDeliveryAttempt?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id)); public Task<NotificationDeliveryAttempt?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult<NotificationDeliveryAttempt?>(null); public Task<(NotificationDeliveryAttempt Attempt, bool IsDuplicate)> AddOrGetDuplicateAsync(NotificationDeliveryAttempt a, CancellationToken ct) => Task.FromResult((a, false)); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByAlertDispatchIdAsync(string u, string a, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>(Items.Where(i => i.UserId == u && i.AlertDispatchId == a).ToArray()); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByUserIdAsync(string u, string? a, string? i, NotificationDeliveryStatus? s, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>([]); public Task<long> CountByUserIdAsync(string u, string? a, string? i, NotificationDeliveryStatus? s, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(NotificationDeliveryAttempt a, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Acks : IAlertAcknowledgementRepository { public List<AlertAcknowledgement> Items { get; } = []; public Task<AlertAcknowledgement?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id)); public Task<AlertAcknowledgement?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult<AlertAcknowledgement?>(null); public Task<(AlertAcknowledgement Acknowledgement, bool IsDuplicate)> AddOrGetDuplicateAsync(AlertAcknowledgement a, CancellationToken ct) => Task.FromResult((a, false)); public Task<IReadOnlyList<AlertAcknowledgement>> ListByAlertDispatchIdAsync(string u, string a, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertAcknowledgement>>(Items.Where(i => i.UserId == u && i.AlertDispatchId == a).ToArray()); public Task<IReadOnlyList<AlertAcknowledgement>> ListByMonitorUserIdAsync(string m, AlertAcknowledgementStatus? s, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertAcknowledgement>>([]); public Task<long> CountByMonitorUserIdAsync(string m, AlertAcknowledgementStatus? s, CancellationToken ct) => Task.FromResult(0L); public Task<IReadOnlyList<AlertAcknowledgement>> ListByUserIdAsync(string u, string? a, string? i, AlertAcknowledgementStatus? s, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertAcknowledgement>>([]); public Task<long> CountByUserIdAsync(string u, string? a, string? i, AlertAcknowledgementStatus? s, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(AlertAcknowledgement a, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Escalations : IEmergencyEscalationRepository { public List<EmergencyEscalation> Items { get; } = []; public Task<EmergencyEscalation?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id)); public Task<EmergencyEscalation?> GetByAlertDispatchIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.AlertDispatchId == id)); public Task<EmergencyEscalation?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.IdempotencyKey == key)); public Task<(EmergencyEscalation Escalation, bool IsDuplicate)> AddOrGetDuplicateAsync(EmergencyEscalation e, CancellationToken ct) { EmergencyEscalation? existing = Items.FirstOrDefault(i => i.AlertDispatchId == e.AlertDispatchId || i.IdempotencyKey == e.IdempotencyKey); if (existing is not null) return Task.FromResult((existing, true)); Items.Add(e); return Task.FromResult((e, false)); } public Task<IReadOnlyList<EmergencyEscalation>> ListAsync(EmergencyEscalationQuery q, CancellationToken ct) => Task.FromResult<IReadOnlyList<EmergencyEscalation>>(Items); public Task<long> CountAsync(EmergencyEscalationQuery q, CancellationToken ct) => Task.FromResult((long)Items.Count); public Task UpdateAsync(EmergencyEscalation e, CancellationToken ct) => Task.CompletedTask; }
}
