using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MotoSOS.API.Modules.AlertDispatch.Application;
using MotoSOS.API.Modules.AlertDispatch.Contracts;
using MotoSOS.API.Modules.AlertDispatch.Domain;
using MotoSOS.API.Modules.Auth.Application;
using MotoSOS.API.Modules.Auth.Contracts;
using MotoSOS.API.Modules.Auth.Domain;
using MotoSOS.API.Modules.Devices.Application;
using MotoSOS.API.Modules.Devices.Domain;
using MotoSOS.API.Modules.EmergencyContacts.Application;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
using MotoSOS.API.Modules.Incidents.Application;
using MotoSOS.API.Modules.Incidents.Domain;
using MotoSOS.API.Modules.OfflineIngestion.Application;
using MotoSOS.API.Modules.OfflineIngestion.Domain;
using MotoSOS.API.Modules.Onboarding.Application;
using MotoSOS.API.Modules.Onboarding.Domain;
using MotoSOS.API.Modules.Plans.Application;
using MotoSOS.API.Modules.Plans.Domain;
using MotoSOS.API.Modules.Profiles.Application;
using MotoSOS.API.Modules.Profiles.Domain;
using MotoSOS.API.Modules.Trips.Application;
using MotoSOS.API.Modules.Trips.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;
using MotoSOS.API.Modules.Vehicles.Application;
using MotoSOS.API.Modules.Vehicles.Domain;

namespace IntegrationTest;

public sealed class AlertDispatchEndpointsTests
{
    [Fact]
    public async Task AlertDispatchRequiresAuthentication()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory(new Stores()); HttpClient client = factory.CreateClient();
        (await client.GetAsync("/api/v1/alert-dispatches")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/v1/alert-dispatches", Request("incident"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("Monitor")]
    [InlineData("Admin")]
    public async Task MonitorAndAdminReceiveForbidden(string role)
    {
        var stores = new Stores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient client = factory.CreateClient(); await AuthenticateAsync(client, $"alert-{role}@example.com", role, stores);
        (await client.GetAsync("/api/v1/alert-dispatches")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RiderCanCreateListGetCancelAndCancelAgain()
    {
        var stores = new Stores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient client = factory.CreateClient(); User user = await AuthenticateAsync(client, "alert-rider@example.com", "Rider", stores); Incident incident = SeedReadyOpenIncident(stores, user.Id);

        AlertEnvelope created = (await (await client.PostAsJsonAsync("/api/v1/alert-dispatches", Request(incident.Id))).Content.ReadFromJsonAsync<AlertEnvelope>())!;
        string list = await (await client.GetAsync("/api/v1/alert-dispatches")).Content.ReadAsStringAsync();
        (await client.GetAsync($"/api/v1/alert-dispatches/{created.Data.AlertDispatch.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        AlertEnvelope cancelled = (await (await client.PostAsJsonAsync($"/api/v1/alert-dispatches/{created.Data.AlertDispatch.Id}/cancel", new CancelAlertDispatchRequest("cancel", DateTimeOffset.UtcNow))).Content.ReadFromJsonAsync<AlertEnvelope>())!;
        AlertEnvelope cancelledAgain = (await (await client.PostAsJsonAsync($"/api/v1/alert-dispatches/{created.Data.AlertDispatch.Id}/cancel", new CancelAlertDispatchRequest("cancel", DateTimeOffset.UtcNow))).Content.ReadFromJsonAsync<AlertEnvelope>())!;

        created.Data.AlertDispatch.Status.Should().Be("PendingDispatch");
        created.Data.AlertDispatch.ContactsCount.Should().Be(1);
        list.Should().Contain(created.Data.AlertDispatch.Id);
        cancelled.Data.AlertDispatch.Status.Should().Be("Cancelled");
        cancelledAgain.Data.AlertDispatch.Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task InvalidReadinessOwnershipAndIncidentStatesReturnExpectedErrors()
    {
        var stores = new Stores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient owner = factory.CreateClient(); HttpClient other = factory.CreateClient(); User ownerUser = await AuthenticateAsync(owner, "alert-owner@example.com", "Rider", stores); User otherUser = await AuthenticateAsync(other, "alert-other@example.com", "Rider", stores); Incident ownerIncident = SeedReadyOpenIncident(stores, ownerUser.Id); Incident otherIncident = SeedReadyOpenIncident(stores, otherUser.Id); Incident closed = Incident(ownerUser.Id, IncidentStatus.Closed); Incident cancelled = Incident(ownerUser.Id, IncidentStatus.FalsePositiveCancelled); stores.Incidents.Items.AddRange([closed, cancelled]);

        HttpResponseMessage foreign = await owner.PostAsJsonAsync("/api/v1/alert-dispatches", Request(otherIncident.Id));
        HttpResponseMessage closedResponse = await owner.PostAsJsonAsync("/api/v1/alert-dispatches", Request(closed.Id));
        HttpResponseMessage cancelledResponse = await owner.PostAsJsonAsync("/api/v1/alert-dispatches", Request(cancelled.Id));
        HttpClient incompleteClient = factory.CreateClient(); User incomplete = await AuthenticateAsync(incompleteClient, "alert-incomplete@example.com", "Rider", stores); Incident incompleteIncident = Incident(incomplete.Id, IncidentStatus.Open); stores.Incidents.Items.Add(incompleteIncident); stores.Contacts.Items.Add(new EmergencyContact { UserId = incomplete.Id, FullName = "Contact", IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Invited }); HttpResponseMessage incompleteResponse = await incompleteClient.PostAsJsonAsync("/api/v1/alert-dispatches", Request(incompleteIncident.Id));

        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
        closedResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest); (await closedResponse.Content.ReadAsStringAsync()).Should().Contain("incident_not_ready");
        cancelledResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest); (await cancelledResponse.Content.ReadAsStringAsync()).Should().Contain("alert_not_allowed");
        incompleteResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest); (await incompleteResponse.Content.ReadAsStringAsync()).Should().Contain("onboarding_not_ready");
    }

    [Fact]
    public async Task DuplicateAndForeignAlertAccessAreHandledSafelyAndCompletedCancelFails()
    {
        var stores = new Stores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient owner = factory.CreateClient(); HttpClient attacker = factory.CreateClient(); User ownerUser = await AuthenticateAsync(owner, "alert-owner2@example.com", "Rider", stores); User attackerUser = await AuthenticateAsync(attacker, "alert-attacker@example.com", "Rider", stores); Incident incident = SeedReadyOpenIncident(stores, ownerUser.Id); SeedReadyOpenIncident(stores, attackerUser.Id); string clientId = Guid.NewGuid().ToString();

        AlertEnvelope first = (await (await owner.PostAsJsonAsync("/api/v1/alert-dispatches", Request(incident.Id, clientId))).Content.ReadFromJsonAsync<AlertEnvelope>())!;
        AlertEnvelope duplicate = (await (await owner.PostAsJsonAsync("/api/v1/alert-dispatches", Request(incident.Id, clientId))).Content.ReadFromJsonAsync<AlertEnvelope>())!;
        var completed = new AlertDispatchRequest { UserId = ownerUser.Id, IncidentId = incident.Id, TripId = incident.TripId, VehicleId = incident.VehicleId, MobileDeviceId = incident.MobileDeviceId, ClientAlertRequestId = Guid.NewGuid().ToString(), IdempotencyKey = Guid.NewGuid().ToString(), Priority = AlertDispatchPriority.High, Reason = AlertDispatchReason.IncidentCreated, Status = AlertDispatchStatus.Completed, RequestedAtUtc = DateTimeOffset.UtcNow, CreatedAtUtc = DateTimeOffset.UtcNow, ContactsSnapshot = [new AlertContactSnapshot { EmergencyContactId = "contact", InvitationStatus = EmergencyContactInvitationStatus.Invited }] }; stores.Alerts.Items.Add(completed);

        duplicate.Data.AlertDispatch.Id.Should().Be(first.Data.AlertDispatch.Id); stores.Alerts.Items.Count(a => a.UserId == ownerUser.Id && a.Status == AlertDispatchStatus.PendingDispatch).Should().Be(1);
        (await attacker.GetAsync($"/api/v1/alert-dispatches/{first.Data.AlertDispatch.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await attacker.PostAsJsonAsync($"/api/v1/alert-dispatches/{first.Data.AlertDispatch.Id}/cancel", new CancelAlertDispatchRequest(null, null))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        HttpResponseMessage completedCancel = await owner.PostAsJsonAsync($"/api/v1/alert-dispatches/{completed.Id}/cancel", new CancelAlertDispatchRequest(null, null));
        completedCancel.StatusCode.Should().Be(HttpStatusCode.BadRequest); (await completedCancel.Content.ReadAsStringAsync()).Should().Contain("alert_dispatch_already_completed");
    }

    private static Incident SeedReadyOpenIncident(Stores stores, string userId) { stores.Profiles.Items.Add(new DriverProfile { UserId = userId, CompletionStatus = ProfileCompletionStatus.Completed }); stores.Vehicles.Items.Add(new DriverVehicle { UserId = userId, IsActive = true, CompletionStatus = VehicleCompletionStatus.Completed }); stores.Contacts.Items.Add(new EmergencyContact { UserId = userId, FullName = "Contact", IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Invited }); stores.Devices.Items.Add(new UserDevice { UserId = userId, DeviceType = DeviceType.MobileApp, IsActive = true, LinkStatus = DeviceLinkStatus.Linked }); stores.Subscriptions.Items.Add(new UserSubscription { UserId = userId, Status = SubscriptionStatus.Active, PlanTier = PlanTier.Basic }); stores.Confirmations.Items.Add(new OnboardingConfirmation { UserId = userId, IsOperational = true }); Incident incident = Incident(userId, IncidentStatus.Open); stores.Incidents.Items.Add(incident); return incident; }
    private static Incident Incident(string userId, IncidentStatus status) => new() { UserId = userId, TripId = "trip", VehicleId = "vehicle", MobileDeviceId = "mobile", SmartwatchDeviceId = "watch", ClientIncidentId = Guid.NewGuid().ToString(), IdempotencyKey = Guid.NewGuid().ToString(), Source = IncidentSource.MobileDetection, Cause = IncidentCause.CountdownTimeout, RiskLevel = IncidentRiskLevel.High, Status = status, OccurredAtUtc = DateTimeOffset.UtcNow, CreatedAtUtc = DateTimeOffset.UtcNow };
    private static CreateAlertDispatchRequest Request(string incidentId, string? clientId = null) => new(incidentId, clientId ?? Guid.NewGuid().ToString(), "High", "IncidentCreated", DateTimeOffset.UtcNow, "prepared");
    private static async Task<User> AuthenticateAsync(HttpClient client, string email, string role, Stores stores) { RegisterRequest register = new(email, "StrongPass1!", "StrongPass1!", "Moto Rider", "+52 555 555 5555", "Rider", true); await client.PostAsJsonAsync("/api/v1/auth/register", register); User user = stores.Users.Items.Single(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)); if (role == "Admin") user.Role = UserRole.Admin; if (role == "Monitor") user.Role = UserRole.Monitor; LoginEnvelope login = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, register.Password))).Content.ReadFromJsonAsync<LoginEnvelope>())!; client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Data.AccessToken); return user; }
    private static WebApplicationFactory<Program> CreateFactory(Stores stores) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Issuer"] = "MotoSOS", ["Jwt:Audience"] = "MotoSOS.Clients", ["Jwt:Key"] = new string('A', 48), ["Jwt:AccessTokenMinutes"] = "15", ["Jwt:RefreshTokenDays"] = "7", ["Jwt:RefreshTokenRememberMeDays"] = "30", ["MongoDb:ConnectionString"] = string.Empty, ["MongoDb:DatabaseName"] = "MotoSOS_Test" })); builder.ConfigureTestServices(s => { s.AddSingleton<IUserRepository>(stores.Users); s.AddSingleton<IRefreshTokenRepository>(stores.RefreshTokens); s.AddSingleton<IDriverProfileRepository>(stores.Profiles); s.AddSingleton<IDriverVehicleRepository>(stores.Vehicles); s.AddSingleton<IEmergencyContactRepository>(stores.Contacts); s.AddSingleton<IDeviceActivationCodeRepository>(stores.Codes); s.AddSingleton<IUserDeviceRepository>(stores.Devices); s.AddSingleton<IUserSubscriptionRepository>(stores.Subscriptions); s.AddSingleton<IOnboardingConfirmationRepository>(stores.Confirmations); s.AddSingleton<ITripRepository>(stores.Trips); s.AddSingleton<IOfflineIngestionRepository>(stores.Offline); s.AddSingleton<IIncidentRepository>(stores.Incidents); s.AddSingleton<IAlertDispatchRepository>(stores.Alerts); }); });
    private sealed class Stores { public Users Users { get; } = new(); public RefreshTokens RefreshTokens { get; } = new(); public Profiles Profiles { get; } = new(); public Vehicles Vehicles { get; } = new(); public Contacts Contacts { get; } = new(); public Codes Codes { get; } = new(); public Devices Devices { get; } = new(); public Subscriptions Subscriptions { get; } = new(); public Confirmations Confirmations { get; } = new(); public Trips Trips { get; } = new(); public Offline Offline { get; } = new(); public Incidents Incidents { get; } = new(); public Alerts Alerts { get; } = new(); }
    private sealed record LoginEnvelope(bool Success, LoginResponse Data); private sealed record AlertEnvelope(bool Success, CreateAlertDispatchResponse Data);
    private sealed class Users : IUserRepository { public List<User> Items { get; } = []; public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Id == id)); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase))); public Task AddAsync(User user, CancellationToken ct) { Items.Add(user); return Task.CompletedTask; } public Task UpdateAsync(User user, CancellationToken ct) => Task.CompletedTask; }
    private sealed class RefreshTokens : IRefreshTokenRepository { public List<RefreshToken> Tokens { get; } = []; public Task<RefreshToken?> GetByHashAsync(string h, CancellationToken ct) => Task.FromResult(Tokens.FirstOrDefault(t => t.TokenHash == h)); public Task AddAsync(RefreshToken t, CancellationToken ct) { Tokens.Add(t); return Task.CompletedTask; } public Task UpdateAsync(RefreshToken t, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Profiles : IDriverProfileRepository { public List<DriverProfile> Items { get; } = []; public Task<DriverProfile?> GetByUserIdAsync(string u, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(p => p.UserId == u)); public Task AddAsync(DriverProfile p, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(DriverProfile p, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Vehicles : IDriverVehicleRepository { public List<DriverVehicle> Items { get; } = []; public Task<IReadOnlyList<DriverVehicle>> GetActiveByUserIdAsync(string u, CancellationToken ct) => Task.FromResult<IReadOnlyList<DriverVehicle>>(Items.Where(v => v.UserId == u && v.IsActive).ToArray()); public Task<DriverVehicle?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(v => v.Id == id)); public Task<int> CountActiveByUserIdAsync(string u, CancellationToken ct) => Task.FromResult(Items.Count(v => v.UserId == u && v.IsActive)); public Task AddAsync(DriverVehicle v, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(DriverVehicle v, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Contacts : IEmergencyContactRepository { public List<EmergencyContact> Items { get; } = []; public Task<IReadOnlyList<EmergencyContact>> GetActiveByUserIdAsync(string u, CancellationToken ct) => Task.FromResult<IReadOnlyList<EmergencyContact>>(Items.Where(c => c.UserId == u && c.IsActive).ToArray()); public Task<EmergencyContact?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(c => c.Id == id)); public Task<EmergencyContact?> GetByLinkingCodeAsync(string code, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(c => c.LinkingCode == code)); public Task<int> CountActiveByUserIdAsync(string u, CancellationToken ct) => Task.FromResult(Items.Count(c => c.UserId == u && c.IsActive)); public Task AddAsync(EmergencyContact c, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(EmergencyContact c, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Codes : IDeviceActivationCodeRepository { public Task<IReadOnlyList<DeviceActivationCode>> GetActiveByUserIdAsync(string u, DateTimeOffset n, CancellationToken ct) => Task.FromResult<IReadOnlyList<DeviceActivationCode>>([]); public Task<DeviceActivationCode?> GetActiveCurrentByUserIdAsync(string u, DateTimeOffset n, CancellationToken ct) => Task.FromResult<DeviceActivationCode?>(null); public Task<DeviceActivationCode?> GetByCodeAsync(string c, CancellationToken ct) => Task.FromResult<DeviceActivationCode?>(null); public Task AddAsync(DeviceActivationCode c, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(DeviceActivationCode c, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Devices : IUserDeviceRepository { public List<UserDevice> Items { get; } = []; public Task<IReadOnlyList<UserDevice>> GetActiveByUserIdAsync(string u, CancellationToken ct) => Task.FromResult<IReadOnlyList<UserDevice>>(Items.Where(d => d.UserId == u && d.IsActive).ToArray()); public Task<IReadOnlyList<UserDevice>> GetActiveByParentDeviceIdAsync(string p, CancellationToken ct) => Task.FromResult<IReadOnlyList<UserDevice>>([]); public Task<UserDevice?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(d => d.Id == id)); public Task<UserDevice?> GetByDeviceIdentifierHashAsync(string u, string h, DeviceType t, CancellationToken ct) => Task.FromResult<UserDevice?>(null); public Task<int> CountActiveLinkedByUserIdAndTypeAsync(string u, DeviceType t, CancellationToken ct) => Task.FromResult(0); public Task<bool> HasActiveLinkedMobileAppAsync(string u, CancellationToken ct) => Task.FromResult(Items.Any(d => d.UserId == u && d.DeviceType == DeviceType.MobileApp && d.IsActive && d.LinkStatus == DeviceLinkStatus.Linked)); public Task AddAsync(UserDevice d, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(UserDevice d, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Subscriptions : IUserSubscriptionRepository { public List<UserSubscription> Items { get; } = []; public Task<UserSubscription?> GetByUserIdAsync(string u, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(s => s.UserId == u)); public Task<bool> HasActiveSubscriptionAsync(string u, CancellationToken ct) => Task.FromResult(Items.Any(s => s.UserId == u && s.Status == SubscriptionStatus.Active)); public Task AddAsync(UserSubscription s, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(UserSubscription s, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Confirmations : IOnboardingConfirmationRepository { public List<OnboardingConfirmation> Items { get; } = []; public Task<OnboardingConfirmation?> GetByUserIdAsync(string u, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(c => c.UserId == u)); public Task AddAsync(OnboardingConfirmation c, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(OnboardingConfirmation c, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Trips : ITripRepository { public Task<Trip?> GetActiveByUserIdAsync(string u, CancellationToken ct) => Task.FromResult<Trip?>(null); public Task<Trip?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult<Trip?>(null); public Task<IReadOnlyList<Trip>> ListByUserIdAsync(string u, TripStatus? s, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<Trip>>([]); public Task<long> CountByUserIdAsync(string u, TripStatus? s, CancellationToken ct) => Task.FromResult(0L); public Task AddAsync(Trip t, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(Trip t, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Offline : IOfflineIngestionRepository { public Task<OfflineIngestionRecord?> GetByIdempotencyKeyAsync(string k, CancellationToken ct) => Task.FromResult<OfflineIngestionRecord?>(null); public Task<(OfflineIngestionRecord Record, bool IsDuplicate)> AddOrGetDuplicateAsync(OfflineIngestionRecord r, CancellationToken ct) => Task.FromResult((r, false)); }
    private sealed class Incidents : IIncidentRepository { public List<Incident> Items { get; } = []; public Task<Incident?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id)); public Task<Incident?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.IdempotencyKey == key)); public Task<(Incident Incident, bool IsDuplicate)> AddOrGetDuplicateAsync(Incident incident, CancellationToken ct) => Task.FromResult((incident, false)); public Task<IReadOnlyList<Incident>> ListByUserIdAsync(string u, IncidentStatus? s, string? t, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<Incident>>(Items.Where(i => i.UserId == u).ToArray()); public Task<long> CountByUserIdAsync(string u, IncidentStatus? s, string? t, CancellationToken ct) => Task.FromResult((long)Items.Count(i => i.UserId == u)); public Task UpdateAsync(Incident i, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Alerts : IAlertDispatchRepository { public List<AlertDispatchRequest> Items { get; } = []; public Task<AlertDispatchRequest?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.Id == id)); public Task<AlertDispatchRequest?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.IdempotencyKey == key)); public Task<(AlertDispatchRequest AlertDispatch, bool IsDuplicate)> AddOrGetDuplicateAsync(AlertDispatchRequest alert, CancellationToken ct) { AlertDispatchRequest? existing = Items.FirstOrDefault(a => a.IdempotencyKey == alert.IdempotencyKey); if (existing is not null) return Task.FromResult((existing, true)); Items.Add(alert); return Task.FromResult((alert, false)); } public Task<IReadOnlyList<AlertDispatchRequest>> ListByUserIdAsync(string u, AlertDispatchStatus? s, string? i, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertDispatchRequest>>(Items.Where(a => a.UserId == u && (!s.HasValue || a.Status == s) && (i is null || a.IncidentId == i)).ToArray()); public Task<long> CountByUserIdAsync(string u, AlertDispatchStatus? s, string? i, CancellationToken ct) => Task.FromResult((long)Items.Count(a => a.UserId == u)); public Task UpdateAsync(AlertDispatchRequest alert, CancellationToken ct) => Task.CompletedTask; }
}
