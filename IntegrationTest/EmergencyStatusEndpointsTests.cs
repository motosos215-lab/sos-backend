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
using MotoSOS.API.Modules.EmergencyContacts.Domain;
using MotoSOS.API.Modules.Incidents.Application;
using MotoSOS.API.Modules.Incidents.Domain;
using MotoSOS.API.Modules.LocationSharing.Application;
using MotoSOS.API.Modules.LocationSharing.Domain;
using MotoSOS.API.Modules.Notifications.Application;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.Trips.Application;
using MotoSOS.API.Modules.Trips.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace IntegrationTest;

public sealed class EmergencyStatusEndpointsTests
{
    [Fact]
    public async Task EmergencyStatusEndpointsRequireAuthentication()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory(new Stores());
        HttpClient client = factory.CreateClient();
        (await client.GetAsync("/api/v1/rider/emergencies/incident/status")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/monitor/alerts/attempt/status")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/rider/emergencies/active")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RiderCanGetOwnStatusAndActiveEmergenciesOnly()
    {
        var stores = new Stores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient owner = factory.CreateClient(); HttpClient other = factory.CreateClient(); User ownerUser = await AuthenticateAsync(owner, "status-owner@example.com", "Rider", stores); User otherUser = await AuthenticateAsync(other, "status-other@example.com", "Rider", stores);
        Incident ownerIncident = SeedEmergency(stores, ownerUser.Id, "incident-owner", IncidentStatus.Open); SeedEmergency(stores, ownerUser.Id, "incident-closed", IncidentStatus.Closed); SeedEmergency(stores, otherUser.Id, "incident-other", IncidentStatus.Open);

        HttpResponseMessage own = await owner.GetAsync($"/api/v1/rider/emergencies/{ownerIncident.Id}/status");
        string active = await (await owner.GetAsync("/api/v1/rider/emergencies/active")).Content.ReadAsStringAsync();
        HttpResponseMessage foreign = await other.GetAsync($"/api/v1/rider/emergencies/{ownerIncident.Id}/status");

        own.StatusCode.Should().Be(HttpStatusCode.OK);
        (await own.Content.ReadAsStringAsync()).Should().Contain("notifications").And.Contain("acknowledgements").And.Contain("location").And.NotContain("deviceIdentifier").And.NotContain("passwordHash");
        active.Should().Contain(ownerIncident.Id).And.NotContain("incident-closed").And.NotContain("incident-other");
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MonitorCanGetAssignedAlertStatusOnlyAndAdminForbidden()
    {
        var stores = new Stores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient monitor = factory.CreateClient(); HttpClient admin = factory.CreateClient(); User monitorUser = await AuthenticateAsync(monitor, "status-monitor@example.com", "Monitor", stores); await AuthenticateAsync(admin, "status-admin@example.com", "Admin", stores);
        Incident incident = SeedEmergency(stores, "rider", "incident-monitor", IncidentStatus.Open); NotificationDeliveryAttempt attempt = stores.Attempts.Items.Single(a => a.IncidentId == incident.Id); stores.Contacts.Items.Add(new EmergencyContact { Id = attempt.EmergencyContactId, LinkedUserId = monitorUser.Id, IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Linked });

        (await monitor.GetAsync($"/api/v1/monitor/alerts/{attempt.Id}/status")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync($"/api/v1/monitor/alerts/{attempt.Id}/status")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        stores.Contacts.Items.Clear();
        (await monitor.GetAsync($"/api/v1/monitor/alerts/{attempt.Id}/status")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 6, 14, 0, 0, TimeSpan.Zero);
    private static Incident SeedEmergency(Stores stores, string userId, string id, IncidentStatus status)
    {
        var trip = new Trip { Id = $"trip-{id}", UserId = userId, Status = TripStatus.Active, StartedAtUtc = Now, CreatedAtUtc = Now }; stores.Trips.Items.Add(trip);
        var incident = new Incident { Id = id, UserId = userId, TripId = trip.Id, Source = IncidentSource.MobileDetection, Cause = IncidentCause.CountdownTimeout, RiskLevel = IncidentRiskLevel.High, Status = status, OccurredAtUtc = Now, CreatedAtUtc = Now }; stores.Incidents.Items.Add(incident);
        var alert = new AlertDispatchRequest { Id = $"alert-{id}", UserId = userId, IncidentId = id, TripId = trip.Id, Priority = AlertDispatchPriority.High, Reason = AlertDispatchReason.IncidentCreated, Status = AlertDispatchStatus.PendingDispatch, CreatedAtUtc = Now }; stores.Alerts.Items.Add(alert);
        stores.Attempts.Items.Add(new NotificationDeliveryAttempt { Id = $"attempt-{id}", UserId = userId, AlertDispatchId = alert.Id, IncidentId = id, TripId = trip.Id, EmergencyContactId = $"contact-{id}", Channel = NotificationChannel.Sms, Status = NotificationDeliveryStatus.Prepared, Provider = NotificationProvider.None, PreparedAtUtc = Now, CreatedAtUtc = Now });
        stores.Acks.Items.Add(new AlertAcknowledgement { UserId = userId, AlertDispatchId = alert.Id, IncidentId = id, TripId = trip.Id, EmergencyContactId = $"contact-{id}", NotificationDeliveryAttemptId = $"attempt-{id}", Status = AlertAcknowledgementStatus.Pending, CreatedAtUtc = Now });
        stores.Locations.Item = new EmergencyLocationSnapshot { UserId = userId, IncidentId = id, TripId = trip.Id, Latitude = 19, Longitude = -99, Source = LocationSharingSource.MobileApp, ClientLocationUpdateId = Guid.NewGuid().ToString(), RecordedAtUtc = Now, ReceivedAtUtc = Now, IsActive = true };
        return incident;
    }
    private static async Task<User> AuthenticateAsync(HttpClient client, string email, string role, Stores stores) { var register = new RegisterRequest(email, "StrongPass1!", "StrongPass1!", "Moto Rider", "+52 555", "Rider", true); await client.PostAsJsonAsync("/api/v1/auth/register", register); User user = stores.Users.Items.Single(u => u.Email == email); if (role == "Monitor") user.Role = UserRole.Monitor; if (role == "Admin") user.Role = UserRole.Admin; LoginEnvelope login = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, register.Password))).Content.ReadFromJsonAsync<LoginEnvelope>())!; client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Data.AccessToken); return user; }
    private static WebApplicationFactory<Program> CreateFactory(Stores stores) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Issuer"] = "MotoSOS", ["Jwt:Audience"] = "MotoSOS.Clients", ["Jwt:Key"] = new string('E', 48), ["Jwt:AccessTokenMinutes"] = "15", ["Jwt:RefreshTokenDays"] = "7", ["Jwt:RefreshTokenRememberMeDays"] = "30", ["MongoDb:ConnectionString"] = string.Empty, ["MongoDb:DatabaseName"] = "MotoSOS_Test" })); builder.ConfigureTestServices(services => { services.AddSingleton<IUserRepository>(stores.Users); services.AddSingleton<IRefreshTokenRepository>(stores.RefreshTokens); services.AddSingleton<IIncidentRepository>(stores.Incidents); services.AddSingleton<ITripRepository>(stores.Trips); services.AddSingleton<IAlertDispatchRepository>(stores.Alerts); services.AddSingleton<INotificationDeliveryAttemptRepository>(stores.Attempts); services.AddSingleton<INotificationAttemptMonitorRepository>(stores.Attempts); services.AddSingleton<IAlertAcknowledgementRepository>(stores.Acks); services.AddSingleton<ILocationSharingRepository>(stores.Locations); services.AddSingleton<IMonitorLinkedContactRepository>(stores.Contacts); }); });
    private sealed record LoginEnvelope(bool Success, LoginResponse Data);
    private sealed class Stores { public Users Users { get; } = new(); public RefreshTokens RefreshTokens { get; } = new(); public Incidents Incidents { get; } = new(); public Trips Trips { get; } = new(); public Alerts Alerts { get; } = new(); public Attempts Attempts { get; } = new(); public Acks Acks { get; } = new(); public Locations Locations { get; } = new(); public Contacts Contacts { get; } = new(); }
    private sealed class Users : IUserRepository { public List<User> Items { get; } = []; public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Id == id)); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Email == email)); public Task AddAsync(User u, CancellationToken ct) { Items.Add(u); return Task.CompletedTask; } public Task UpdateAsync(User u, CancellationToken ct) => Task.CompletedTask; }
    private sealed class RefreshTokens : IRefreshTokenRepository { public List<RefreshToken> Items { get; } = []; public Task<RefreshToken?> GetByHashAsync(string h, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.TokenHash == h)); public Task AddAsync(RefreshToken t, CancellationToken ct) { Items.Add(t); return Task.CompletedTask; } public Task UpdateAsync(RefreshToken t, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Incidents : IIncidentRepository { public List<Incident> Items { get; } = []; public Task<Incident?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id)); public Task<Incident?> GetByIdempotencyKeyAsync(string k, CancellationToken ct) => Task.FromResult<Incident?>(null); public Task<(Incident Incident, bool IsDuplicate)> AddOrGetDuplicateAsync(Incident i, CancellationToken ct) => Task.FromResult((i, false)); public Task<IReadOnlyList<Incident>> ListByUserIdAsync(string u, IncidentStatus? s, string? t, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<Incident>>(Items.Where(i => i.UserId == u && (!s.HasValue || i.Status == s)).Skip((p - 1) * z).Take(z).ToArray()); public Task<long> CountByUserIdAsync(string u, IncidentStatus? s, string? t, CancellationToken ct) => Task.FromResult((long)Items.Count(i => i.UserId == u && (!s.HasValue || i.Status == s))); public Task UpdateAsync(Incident i, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Trips : ITripRepository { public List<Trip> Items { get; } = []; public Task<Trip?> GetActiveByUserIdAsync(string u, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.UserId == u && t.Status == TripStatus.Active)); public Task<Trip?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.Id == id)); public Task<IReadOnlyList<Trip>> ListByUserIdAsync(string u, TripStatus? s, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<Trip>>([]); public Task<long> CountByUserIdAsync(string u, TripStatus? s, CancellationToken ct) => Task.FromResult(0L); public Task AddAsync(Trip t, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(Trip t, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Alerts : IAlertDispatchRepository { public List<AlertDispatchRequest> Items { get; } = []; public Task<AlertDispatchRequest?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.Id == id)); public Task<AlertDispatchRequest?> GetByIdempotencyKeyAsync(string k, CancellationToken ct) => Task.FromResult<AlertDispatchRequest?>(null); public Task<(AlertDispatchRequest AlertDispatch, bool IsDuplicate)> AddOrGetDuplicateAsync(AlertDispatchRequest a, CancellationToken ct) => Task.FromResult((a, false)); public Task<IReadOnlyList<AlertDispatchRequest>> ListByIncidentIdAsync(string u, string i, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertDispatchRequest>>(Items.Where(a => a.UserId == u && a.IncidentId == i).ToArray()); public Task<IReadOnlyList<AlertDispatchRequest>> ListByUserIdAsync(string u, AlertDispatchStatus? s, string? i, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertDispatchRequest>>([]); public Task<long> CountByUserIdAsync(string u, AlertDispatchStatus? s, string? i, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(AlertDispatchRequest a, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Attempts : INotificationDeliveryAttemptRepository, INotificationAttemptMonitorRepository { public List<NotificationDeliveryAttempt> Items { get; } = []; public Task<NotificationDeliveryAttempt?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.Id == id)); public Task<NotificationDeliveryAttempt?> GetByIdempotencyKeyAsync(string k, CancellationToken ct) => Task.FromResult<NotificationDeliveryAttempt?>(null); public Task<(NotificationDeliveryAttempt Attempt, bool IsDuplicate)> AddOrGetDuplicateAsync(NotificationDeliveryAttempt a, CancellationToken ct) => Task.FromResult((a, false)); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByIncidentIdAsync(string u, string i, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>(Items.Where(a => a.UserId == u && a.IncidentId == i).ToArray()); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByAlertDispatchIdAsync(string u, string a, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>(Items.Where(n => n.UserId == u && n.AlertDispatchId == a).ToArray()); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByUserIdAsync(string u, string? a, string? i, NotificationDeliveryStatus? s, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>([]); public Task<long> CountByUserIdAsync(string u, string? a, string? i, NotificationDeliveryStatus? s, CancellationToken ct) => Task.FromResult(0L); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByEmergencyContactIdsAsync(IReadOnlyCollection<string> ids, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>(Items.Where(a => ids.Contains(a.EmergencyContactId)).ToArray()); public Task<long> CountByEmergencyContactIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(NotificationDeliveryAttempt a, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Acks : IAlertAcknowledgementRepository { public List<AlertAcknowledgement> Items { get; } = []; public Task<AlertAcknowledgement?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.Id == id)); public Task<AlertAcknowledgement?> GetByIdempotencyKeyAsync(string k, CancellationToken ct) => Task.FromResult<AlertAcknowledgement?>(null); public Task<(AlertAcknowledgement Acknowledgement, bool IsDuplicate)> AddOrGetDuplicateAsync(AlertAcknowledgement a, CancellationToken ct) => Task.FromResult((a, false)); public Task<IReadOnlyList<AlertAcknowledgement>> ListByIncidentIdAsync(string u, string i, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertAcknowledgement>>(Items.Where(a => a.UserId == u && a.IncidentId == i).ToArray()); public Task<IReadOnlyList<AlertAcknowledgement>> ListByAlertDispatchIdAsync(string u, string a, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertAcknowledgement>>(Items.Where(n => n.UserId == u && n.AlertDispatchId == a).ToArray()); public Task<IReadOnlyList<AlertAcknowledgement>> ListByMonitorUserIdAsync(string m, AlertAcknowledgementStatus? s, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertAcknowledgement>>([]); public Task<long> CountByMonitorUserIdAsync(string m, AlertAcknowledgementStatus? s, CancellationToken ct) => Task.FromResult(0L); public Task<IReadOnlyList<AlertAcknowledgement>> ListByUserIdAsync(string u, string? a, string? i, AlertAcknowledgementStatus? s, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertAcknowledgement>>([]); public Task<long> CountByUserIdAsync(string u, string? a, string? i, AlertAcknowledgementStatus? s, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(AlertAcknowledgement a, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Locations : ILocationSharingRepository { public EmergencyLocationSnapshot? Item { get; set; } public Task<EmergencyLocationSnapshot?> GetByUserIdAndIncidentIdAsync(string u, string i, CancellationToken ct) => Task.FromResult(Item); public Task<EmergencyLocationSnapshot?> GetActiveByIncidentIdAsync(string i, CancellationToken ct) => Task.FromResult(Item is { IncidentId: var id, IsActive: true } l && id == i ? l : null); public Task<EmergencyLocationSnapshot> UpsertLatestAsync(EmergencyLocationSnapshot s, CancellationToken ct) => Task.FromResult(s); }
    private sealed class Contacts : IMonitorLinkedContactRepository { public List<EmergencyContact> Items { get; } = []; public Task<IReadOnlyList<EmergencyContact>> GetActiveLinkedByLinkedUserIdAsync(string id, CancellationToken ct) => Task.FromResult<IReadOnlyList<EmergencyContact>>(Items.Where(c => c.LinkedUserId == id && c.IsActive && c.InvitationStatus == EmergencyContactInvitationStatus.Linked).ToArray()); }
}
