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
using MotoSOS.API.Modules.EmergencyResolution.Application;
using MotoSOS.API.Modules.EmergencyResolution.Contracts;
using MotoSOS.API.Modules.EmergencyResolution.Domain;
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

public sealed class EmergencyResolutionEndpointsTests
{
    [Fact]
    public async Task EmergencyResolutionEndpointsRequireAuthentication()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory(new Stores());
        HttpClient client = factory.CreateClient();
        (await client.PostAsJsonAsync("/api/v1/rider/emergencies/incident/resolution-report", new CreateEmergencyResolutionReportRequest("RealEmergency", "Resolved", null))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/rider/emergencies/incident/resolution-report")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/rider/emergencies/resolution-reports")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/monitor/alerts/attempt/resolution-report")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RiderCreatesIdempotentClosedReportAndCanListOwnReportsOnly()
    {
        var stores = new Stores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient owner = factory.CreateClient(); HttpClient other = factory.CreateClient(); User ownerUser = await AuthenticateAsync(owner, "resolution-owner@example.com", UserRole.Rider, stores); User otherUser = await AuthenticateAsync(other, "resolution-other@example.com", UserRole.Rider, stores);
        Incident ownerIncident = SeedEmergency(stores, ownerUser.Id, "resolution-owner", IncidentStatus.Closed); SeedEmergency(stores, otherUser.Id, "resolution-other", IncidentStatus.Closed);

        HttpResponseMessage create = await owner.PostAsJsonAsync($"/api/v1/rider/emergencies/{ownerIncident.Id}/resolution-report", new CreateEmergencyResolutionReportRequest("RealEmergency", "Resolved safely", "No sensitive data"));
        HttpResponseMessage duplicate = await owner.PostAsJsonAsync($"/api/v1/rider/emergencies/{ownerIncident.Id}/resolution-report", new CreateEmergencyResolutionReportRequest("FalsePositive", "Different summary", null));
        string get = await (await owner.GetAsync($"/api/v1/rider/emergencies/{ownerIncident.Id}/resolution-report")).Content.ReadAsStringAsync();
        string list = await (await owner.GetAsync("/api/v1/rider/emergencies/resolution-reports")).Content.ReadAsStringAsync();
        HttpResponseMessage foreign = await other.GetAsync($"/api/v1/rider/emergencies/{ownerIncident.Id}/resolution-report");

        create.StatusCode.Should().Be(HttpStatusCode.OK);
        duplicate.StatusCode.Should().Be(HttpStatusCode.OK);
        stores.Reports.Items.Should().HaveCount(1);
        get.Should().Contain("notificationAttemptsTotal").And.Contain("acknowledgedCount").And.Contain("finalLatitude").And.NotContain("password" + "Hash").And.NotContain("refresh" + "Token").And.NotContain("device" + "Identifier");
        list.Should().Contain(ownerIncident.Id).And.NotContain("resolution-other");
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OpenIncidentAdminAndMonitorCreateAreRejected()
    {
        var stores = new Stores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient rider = factory.CreateClient(); HttpClient admin = factory.CreateClient(); HttpClient monitor = factory.CreateClient(); User riderUser = await AuthenticateAsync(rider, "resolution-open@example.com", UserRole.Rider, stores); await AuthenticateAsync(admin, "resolution-admin@example.com", UserRole.Admin, stores); await AuthenticateAsync(monitor, "resolution-monitor-create@example.com", UserRole.Monitor, stores);
        Incident open = SeedEmergency(stores, riderUser.Id, "resolution-open", IncidentStatus.Open);

        (await rider.PostAsJsonAsync($"/api/v1/rider/emergencies/{open.Id}/resolution-report", new CreateEmergencyResolutionReportRequest("RealEmergency", "Resolved", null))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync($"/api/v1/rider/emergencies/{open.Id}/resolution-report", new CreateEmergencyResolutionReportRequest("RealEmergency", "Resolved", null))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await monitor.PostAsJsonAsync($"/api/v1/rider/emergencies/{open.Id}/resolution-report", new CreateEmergencyResolutionReportRequest("RealEmergency", "Resolved", null))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AssignedMonitorCanReadResolutionReportOnlyForAssignedAlert()
    {
        var stores = new Stores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient rider = factory.CreateClient(); HttpClient monitor = factory.CreateClient(); User riderUser = await AuthenticateAsync(rider, "resolution-rider@example.com", UserRole.Rider, stores); User monitorUser = await AuthenticateAsync(monitor, "resolution-monitor@example.com", UserRole.Monitor, stores);
        Incident incident = SeedEmergency(stores, riderUser.Id, "resolution-monitor", IncidentStatus.FalsePositiveCancelled); NotificationDeliveryAttempt attempt = stores.Attempts.Items.Single(a => a.IncidentId == incident.Id); await rider.PostAsJsonAsync($"/api/v1/rider/emergencies/{incident.Id}/resolution-report", new CreateEmergencyResolutionReportRequest("FalsePositive", "False alarm", null));
        stores.Contacts.Items.Add(new EmergencyContact { Id = attempt.EmergencyContactId, LinkedUserId = monitorUser.Id, IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Linked });

        (await monitor.GetAsync($"/api/v1/monitor/alerts/{attempt.Id}/resolution-report")).StatusCode.Should().Be(HttpStatusCode.OK);
        stores.Contacts.Items.Clear();
        (await monitor.GetAsync($"/api/v1/monitor/alerts/{attempt.Id}/resolution-report")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);
    private static Incident SeedEmergency(Stores stores, string userId, string id, IncidentStatus status)
    {
        var trip = new Trip { Id = $"trip-{id}", UserId = userId, Status = TripStatus.Finished, StartedAtUtc = Now.AddMinutes(-30), FinishedAtUtc = Now, CreatedAtUtc = Now.AddMinutes(-30) }; stores.Trips.Items.Add(trip);
        var incident = new Incident { Id = id, UserId = userId, TripId = trip.Id, Source = IncidentSource.MobileDetection, Cause = IncidentCause.CountdownTimeout, RiskLevel = IncidentRiskLevel.High, Status = status, OccurredAtUtc = Now.AddMinutes(-10), CreatedAtUtc = Now.AddMinutes(-10), ClosedAtUtc = status == IncidentStatus.Closed ? Now : null, CancelledAtUtc = status == IncidentStatus.FalsePositiveCancelled ? Now : null, ClosedByUserId = userId }; stores.Incidents.Items.Add(incident);
        var alert = new AlertDispatchRequest { Id = $"alert-{id}", UserId = userId, IncidentId = id, TripId = trip.Id, Priority = AlertDispatchPriority.High, Reason = AlertDispatchReason.IncidentCreated, Status = AlertDispatchStatus.Completed, CreatedAtUtc = Now.AddMinutes(-9), RequestedAtUtc = Now.AddMinutes(-9) }; stores.Alerts.Items.Add(alert);
        stores.Attempts.Items.Add(new NotificationDeliveryAttempt { Id = $"attempt-{id}", UserId = userId, AlertDispatchId = alert.Id, IncidentId = id, TripId = trip.Id, EmergencyContactId = $"contact-{id}", Channel = NotificationChannel.Sms, Status = NotificationDeliveryStatus.Prepared, Provider = NotificationProvider.None, PreparedAtUtc = Now.AddMinutes(-8), CreatedAtUtc = Now.AddMinutes(-8) });
        stores.Acks.Items.Add(new AlertAcknowledgement { UserId = userId, AlertDispatchId = alert.Id, IncidentId = id, TripId = trip.Id, EmergencyContactId = $"contact-{id}", NotificationDeliveryAttemptId = $"attempt-{id}", Status = AlertAcknowledgementStatus.Acknowledged, AcknowledgedAtUtc = Now.AddMinutes(-5), CreatedAtUtc = Now.AddMinutes(-6) });
        stores.Locations.Items.Add(new EmergencyLocationSnapshot { UserId = userId, IncidentId = id, TripId = trip.Id, Latitude = 19, Longitude = -99, Source = LocationSharingSource.MobileApp, ClientLocationUpdateId = $"location-{id}", RecordedAtUtc = Now.AddMinutes(-4), ReceivedAtUtc = Now.AddMinutes(-3), IsActive = false });
        return incident;
    }
    private static async Task<User> AuthenticateAsync(HttpClient client, string email, UserRole role, Stores stores) { var register = new RegisterRequest(email, "StrongPass1!", "StrongPass1!", "Moto Rider", "+52 555", "Rider", true); await client.PostAsJsonAsync("/api/v1/auth/register", register); User user = stores.Users.Items.Single(u => u.Email == email); user.Role = role; LoginEnvelope login = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, register.Password))).Content.ReadFromJsonAsync<LoginEnvelope>())!; client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Data.AccessToken); return user; }
    private static WebApplicationFactory<Program> CreateFactory(Stores stores) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Issuer"] = "MotoSOS", ["Jwt:Audience"] = "MotoSOS.Clients", ["Jwt:Key"] = new string('R', 48), ["Jwt:AccessTokenMinutes"] = "15", ["Jwt:RefreshTokenDays"] = "7", ["Jwt:RefreshTokenRememberMeDays"] = "30", ["MongoDb:" + "Connection" + "String"] = string.Empty, ["MongoDb:DatabaseName"] = "MotoSOS_Test" })); builder.ConfigureTestServices(services => { services.AddSingleton<IUserRepository>(stores.Users); services.AddSingleton<IRefreshTokenRepository>(stores.RefreshTokens); services.AddSingleton<IIncidentRepository>(stores.Incidents); services.AddSingleton<ITripRepository>(stores.Trips); services.AddSingleton<IAlertDispatchRepository>(stores.Alerts); services.AddSingleton<INotificationDeliveryAttemptRepository>(stores.Attempts); services.AddSingleton<INotificationAttemptMonitorRepository>(stores.Attempts); services.AddSingleton<IAlertAcknowledgementRepository>(stores.Acks); services.AddSingleton<ILocationSharingRepository>(stores.Locations); services.AddSingleton<IMonitorLinkedContactRepository>(stores.Contacts); services.AddSingleton<IEmergencyResolutionRepository>(stores.Reports); }); });
    private sealed record LoginEnvelope(bool Success, LoginResponse Data);
    private sealed class Stores { public Users Users { get; } = new(); public RefreshTokens RefreshTokens { get; } = new(); public Incidents Incidents { get; } = new(); public Trips Trips { get; } = new(); public Alerts Alerts { get; } = new(); public Attempts Attempts { get; } = new(); public Acks Acks { get; } = new(); public Locations Locations { get; } = new(); public Contacts Contacts { get; } = new(); public Reports Reports { get; } = new(); }
    private sealed class Users : IUserRepository { public List<User> Items { get; } = []; public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Id == id)); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Email == email)); public Task AddAsync(User u, CancellationToken ct) { Items.Add(u); return Task.CompletedTask; } public Task UpdateAsync(User u, CancellationToken ct) => Task.CompletedTask; }
    private sealed class RefreshTokens : IRefreshTokenRepository { public List<RefreshToken> Items { get; } = []; public Task<RefreshToken?> GetByHashAsync(string h, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.TokenHash == h)); public Task AddAsync(RefreshToken t, CancellationToken ct) { Items.Add(t); return Task.CompletedTask; } public Task UpdateAsync(RefreshToken t, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Incidents : IIncidentRepository { public List<Incident> Items { get; } = []; public Task<Incident?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id)); public Task<Incident?> GetByIdempotencyKeyAsync(string k, CancellationToken ct) => Task.FromResult<Incident?>(null); public Task<(Incident Incident, bool IsDuplicate)> AddOrGetDuplicateAsync(Incident i, CancellationToken ct) => Task.FromResult((i, false)); public Task<IReadOnlyList<Incident>> ListByUserIdAsync(string u, IncidentStatus? s, string? t, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<Incident>>(Items.Where(i => i.UserId == u && (!s.HasValue || i.Status == s)).ToArray()); public Task<long> CountByUserIdAsync(string u, IncidentStatus? s, string? t, CancellationToken ct) => Task.FromResult((long)Items.Count(i => i.UserId == u && (!s.HasValue || i.Status == s))); public Task UpdateAsync(Incident i, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Trips : ITripRepository { public List<Trip> Items { get; } = []; public Task<Trip?> GetActiveByUserIdAsync(string u, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.UserId == u && t.Status == TripStatus.Active)); public Task<Trip?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.Id == id)); public Task<IReadOnlyList<Trip>> ListByUserIdAsync(string u, TripStatus? s, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<Trip>>([]); public Task<long> CountByUserIdAsync(string u, TripStatus? s, CancellationToken ct) => Task.FromResult(0L); public Task AddAsync(Trip t, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(Trip t, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Alerts : IAlertDispatchRepository { public List<AlertDispatchRequest> Items { get; } = []; public Task<AlertDispatchRequest?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.Id == id)); public Task<AlertDispatchRequest?> GetByIdempotencyKeyAsync(string k, CancellationToken ct) => Task.FromResult<AlertDispatchRequest?>(null); public Task<(AlertDispatchRequest AlertDispatch, bool IsDuplicate)> AddOrGetDuplicateAsync(AlertDispatchRequest a, CancellationToken ct) => Task.FromResult((a, false)); public Task<IReadOnlyList<AlertDispatchRequest>> ListByIncidentIdAsync(string u, string i, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertDispatchRequest>>(Items.Where(a => a.UserId == u && a.IncidentId == i).ToArray()); public Task<IReadOnlyList<AlertDispatchRequest>> ListByUserIdAsync(string u, AlertDispatchStatus? s, string? i, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertDispatchRequest>>([]); public Task<long> CountByUserIdAsync(string u, AlertDispatchStatus? s, string? i, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(AlertDispatchRequest a, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Attempts : INotificationDeliveryAttemptRepository, INotificationAttemptMonitorRepository { public List<NotificationDeliveryAttempt> Items { get; } = []; public Task<NotificationDeliveryAttempt?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.Id == id)); public Task<NotificationDeliveryAttempt?> GetByIdempotencyKeyAsync(string k, CancellationToken ct) => Task.FromResult<NotificationDeliveryAttempt?>(null); public Task<(NotificationDeliveryAttempt Attempt, bool IsDuplicate)> AddOrGetDuplicateAsync(NotificationDeliveryAttempt a, CancellationToken ct) => Task.FromResult((a, false)); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByIncidentIdAsync(string u, string i, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>(Items.Where(a => a.UserId == u && a.IncidentId == i).ToArray()); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByAlertDispatchIdAsync(string u, string a, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>(Items.Where(n => n.UserId == u && n.AlertDispatchId == a).ToArray()); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByUserIdAsync(string u, string? a, string? i, NotificationDeliveryStatus? s, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>([]); public Task<long> CountByUserIdAsync(string u, string? a, string? i, NotificationDeliveryStatus? s, CancellationToken ct) => Task.FromResult(0L); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByEmergencyContactIdsAsync(IReadOnlyCollection<string> ids, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>(Items.Where(a => ids.Contains(a.EmergencyContactId)).ToArray()); public Task<long> CountByEmergencyContactIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(NotificationDeliveryAttempt a, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Acks : IAlertAcknowledgementRepository { public List<AlertAcknowledgement> Items { get; } = []; public Task<AlertAcknowledgement?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.Id == id)); public Task<AlertAcknowledgement?> GetByIdempotencyKeyAsync(string k, CancellationToken ct) => Task.FromResult<AlertAcknowledgement?>(null); public Task<(AlertAcknowledgement Acknowledgement, bool IsDuplicate)> AddOrGetDuplicateAsync(AlertAcknowledgement a, CancellationToken ct) => Task.FromResult((a, false)); public Task<IReadOnlyList<AlertAcknowledgement>> ListByIncidentIdAsync(string u, string i, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertAcknowledgement>>(Items.Where(a => a.UserId == u && a.IncidentId == i).ToArray()); public Task<IReadOnlyList<AlertAcknowledgement>> ListByAlertDispatchIdAsync(string u, string a, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertAcknowledgement>>(Items.Where(n => n.UserId == u && n.AlertDispatchId == a).ToArray()); public Task<IReadOnlyList<AlertAcknowledgement>> ListByMonitorUserIdAsync(string m, AlertAcknowledgementStatus? s, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertAcknowledgement>>([]); public Task<long> CountByMonitorUserIdAsync(string m, AlertAcknowledgementStatus? s, CancellationToken ct) => Task.FromResult(0L); public Task<IReadOnlyList<AlertAcknowledgement>> ListByUserIdAsync(string u, string? a, string? i, AlertAcknowledgementStatus? s, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertAcknowledgement>>([]); public Task<long> CountByUserIdAsync(string u, string? a, string? i, AlertAcknowledgementStatus? s, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(AlertAcknowledgement a, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Locations : ILocationSharingRepository { public List<EmergencyLocationSnapshot> Items { get; } = []; public Task<EmergencyLocationSnapshot?> GetByUserIdAndIncidentIdAsync(string u, string i, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(l => l.UserId == u && l.IncidentId == i)); public Task<EmergencyLocationSnapshot?> GetActiveByIncidentIdAsync(string i, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(l => l.IncidentId == i && l.IsActive)); public Task<EmergencyLocationSnapshot?> GetLatestByIncidentIdAsync(string i, CancellationToken ct) => Task.FromResult(Items.Where(l => l.IncidentId == i).OrderByDescending(l => l.RecordedAtUtc).ThenByDescending(l => l.ReceivedAtUtc).FirstOrDefault()); public Task<EmergencyLocationSnapshot> UpsertLatestAsync(EmergencyLocationSnapshot s, CancellationToken ct) { Items.RemoveAll(l => l.UserId == s.UserId && l.IncidentId == s.IncidentId); Items.Add(s); return Task.FromResult(s); } }
    private sealed class Contacts : IMonitorLinkedContactRepository { public List<EmergencyContact> Items { get; } = []; public Task<IReadOnlyList<EmergencyContact>> GetActiveLinkedByLinkedUserIdAsync(string id, CancellationToken ct) => Task.FromResult<IReadOnlyList<EmergencyContact>>(Items.Where(c => c.LinkedUserId == id && c.IsActive && c.InvitationStatus == EmergencyContactInvitationStatus.Linked).ToArray()); }
    private sealed class Reports : IEmergencyResolutionRepository { public List<EmergencyResolutionReport> Items { get; } = []; public Task<EmergencyResolutionReport?> GetByIncidentIdAsync(string i, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(r => r.IncidentId == i)); public Task<EmergencyResolutionReport?> GetByIdempotencyKeyAsync(string k, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(r => r.IdempotencyKey == k)); public Task<(EmergencyResolutionReport Report, bool IsDuplicate)> AddOrGetDuplicateAsync(EmergencyResolutionReport r, CancellationToken ct) { EmergencyResolutionReport? existing = Items.FirstOrDefault(x => x.IdempotencyKey == r.IdempotencyKey || x.IncidentId == r.IncidentId); if (existing is not null) return Task.FromResult((existing, true)); Items.Add(r); return Task.FromResult((r, false)); } public Task<IReadOnlyList<EmergencyResolutionReport>> ListByUserIdAsync(string u, EmergencyResolutionOutcome? o, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<EmergencyResolutionReport>>(Items.Where(r => r.UserId == u && (!o.HasValue || r.Outcome == o)).Skip((p - 1) * z).Take(z).ToArray()); public Task<long> CountByUserIdAsync(string u, EmergencyResolutionOutcome? o, CancellationToken ct) => Task.FromResult((long)Items.Count(r => r.UserId == u && (!o.HasValue || r.Outcome == o))); }
}
