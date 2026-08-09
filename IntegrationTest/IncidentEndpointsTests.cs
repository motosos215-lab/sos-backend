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
using MotoSOS.API.Modules.EmergencyContacts.Application;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
using MotoSOS.API.Modules.Incidents.Application;
using MotoSOS.API.Modules.Incidents.Contracts;
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

public sealed class IncidentEndpointsTests
{
    [Fact]
    public async Task IncidentsRequireAuthentication()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory(new TestStores());
        HttpClient client = factory.CreateClient();
        (await client.GetAsync("/api/v1/incidents")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/v1/incidents", Request("trip"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("Monitor")]
    [InlineData("Admin")]
    public async Task NonRidersReceiveForbidden(string role)
    {
        var stores = new TestStores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient client = factory.CreateClient(); await AuthenticateAsync(client, $"incident-{role}@example.com", role, stores);
        (await client.GetAsync("/api/v1/incidents")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RiderCanCreateListGetCancelAndCloseIncident()
    {
        var stores = new TestStores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient client = factory.CreateClient(); User user = await AuthenticateAsync(client, "incident-rider@example.com", "Rider", stores); Trip trip = SeedReady(stores, user.Id);
        IncidentEnvelope created = (await (await client.PostAsJsonAsync("/api/v1/incidents", Request(trip.Id))).Content.ReadFromJsonAsync<IncidentEnvelope>())!;
        string list = await (await client.GetAsync("/api/v1/incidents")).Content.ReadAsStringAsync();
        (await client.GetAsync($"/api/v1/incidents/{created.Data.Incident.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        IncidentEnvelope cancelled = (await (await client.PostAsJsonAsync($"/api/v1/incidents/{created.Data.Incident.Id}/cancel-false-positive", new CancelFalsePositiveRequest("Estoy bien", DateTimeOffset.UtcNow))).Content.ReadFromJsonAsync<IncidentEnvelope>())!;
        IncidentEnvelope closed = (await (await client.PostAsJsonAsync($"/api/v1/incidents/{created.Data.Incident.Id}/close", new CloseIncidentRequest("Resolved", "done", DateTimeOffset.UtcNow))).Content.ReadFromJsonAsync<IncidentEnvelope>())!;
        HttpResponseMessage cancelClosed = await client.PostAsJsonAsync($"/api/v1/incidents/{created.Data.Incident.Id}/cancel-false-positive", new CancelFalsePositiveRequest(null, null));
        created.Data.Incident.Status.Should().Be("Open"); list.Should().Contain(created.Data.Incident.Id); cancelled.Data.Incident.Status.Should().Be("FalsePositiveCancelled"); closed.Data.Incident.Status.Should().Be("Closed"); cancelClosed.StatusCode.Should().Be(HttpStatusCode.BadRequest); (await cancelClosed.Content.ReadAsStringAsync()).Should().Contain("incident_already_closed");
    }

    [Fact]
    public async Task DuplicateAndForeignTripAreHandledSafely()
    {
        var stores = new TestStores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient owner = factory.CreateClient(); HttpClient otherClient = factory.CreateClient(); User ownerUser = await AuthenticateAsync(owner, "incident-owner@example.com", "Rider", stores); User otherUser = await AuthenticateAsync(otherClient, "incident-other@example.com", "Rider", stores); Trip ownerTrip = SeedReady(stores, ownerUser.Id); Trip otherTrip = SeedReady(stores, otherUser.Id); string clientIncidentId = Guid.NewGuid().ToString();
        IncidentEnvelope first = (await (await owner.PostAsJsonAsync("/api/v1/incidents", Request(ownerTrip.Id, clientIncidentId))).Content.ReadFromJsonAsync<IncidentEnvelope>())!;
        IncidentEnvelope duplicate = (await (await owner.PostAsJsonAsync("/api/v1/incidents", Request(ownerTrip.Id, clientIncidentId))).Content.ReadFromJsonAsync<IncidentEnvelope>())!;
        HttpResponseMessage foreign = await owner.PostAsJsonAsync("/api/v1/incidents", Request(otherTrip.Id));
        duplicate.Data.Incident.Id.Should().Be(first.Data.Incident.Id); stores.Incidents.Items.Should().HaveCount(1); foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task IncompleteOnboardingCannotCreateIncident()
    {
        var stores = new TestStores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient client = factory.CreateClient(); User user = await AuthenticateAsync(client, "incident-incomplete@example.com", "Rider", stores); Trip trip = new() { UserId = user.Id, VehicleId = "vehicle", MobileDeviceId = "mobile", Status = TripStatus.Active, StartedAtUtc = DateTimeOffset.UtcNow, CreatedAtUtc = DateTimeOffset.UtcNow }; stores.Trips.Items.Add(trip);
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/incidents", Request(trip.Id));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest); (await response.Content.ReadAsStringAsync()).Should().Contain("onboarding_not_ready");
    }

    private static Trip SeedReady(TestStores stores, string userId) { stores.Profiles.Items.Add(new DriverProfile { UserId = userId, CompletionStatus = ProfileCompletionStatus.Completed }); stores.Vehicles.Items.Add(new DriverVehicle { UserId = userId, IsActive = true, CompletionStatus = VehicleCompletionStatus.Completed }); stores.Contacts.Items.Add(new EmergencyContact { UserId = userId, IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Invited }); stores.Devices.Items.Add(new UserDevice { UserId = userId, DeviceType = DeviceType.MobileApp, IsActive = true, LinkStatus = DeviceLinkStatus.Linked }); stores.Subscriptions.Items.Add(new UserSubscription { UserId = userId, Status = SubscriptionStatus.Active, PlanTier = PlanTier.Basic }); stores.Confirmations.Items.Add(new OnboardingConfirmation { UserId = userId, IsOperational = true }); var trip = new Trip { UserId = userId, VehicleId = "vehicle", MobileDeviceId = "mobile", SmartwatchDeviceId = "watch", Status = TripStatus.Active, StartedAtUtc = DateTimeOffset.UtcNow, CreatedAtUtc = DateTimeOffset.UtcNow }; stores.Trips.Items.Add(trip); return trip; }
    private static CreateIncidentRequest Request(string tripId, string? clientIncidentId = null) => new(tripId, clientIncidentId ?? Guid.NewGuid().ToString(), "MobileDetection", "CountdownTimeout", "High", 87, 0.91, "Good", "rules-v1", "validation-v1", DateTimeOffset.UtcNow, null, null);
    private static async Task<User> AuthenticateAsync(HttpClient client, string email, string accountType, TestStores stores) { RegisterRequest register = new(email, "StrongPass1!", "StrongPass1!", "Moto Rider", "+52 555 555 5555", "Rider", true); await client.PostAsJsonAsync("/api/v1/auth/register", register); User user = stores.Users.Items.Single(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)); if (accountType == "Admin") user.Role = UserRole.Admin; if (accountType == "Monitor") user.Role = UserRole.Monitor; LoginResponse login = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, register.Password))).Content.ReadFromJsonAsync<LoginLoginEnvelope>())!.Data; client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken); return user; }
    private static WebApplicationFactory<Program> CreateFactory(TestStores stores) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Issuer"] = "MotoSOS", ["Jwt:Audience"] = "MotoSOS.Clients", ["Jwt:Key"] = new string('N', 48), ["Jwt:AccessTokenMinutes"] = "15", ["Jwt:RefreshTokenDays"] = "7", ["Jwt:RefreshTokenRememberMeDays"] = "30", ["MongoDb:ConnectionString"] = string.Empty, ["MongoDb:DatabaseName"] = "MotoSOS_Test" })); builder.ConfigureTestServices(services => { services.AddSingleton<IUserRepository>(stores.Users); services.AddSingleton<IRefreshTokenRepository>(stores.RefreshTokens); services.AddSingleton<IDriverProfileRepository>(stores.Profiles); services.AddSingleton<IDriverVehicleRepository>(stores.Vehicles); services.AddSingleton<IEmergencyContactRepository>(stores.Contacts); services.AddSingleton<IDeviceActivationCodeRepository>(stores.Codes); services.AddSingleton<IUserDeviceRepository>(stores.Devices); services.AddSingleton<IUserSubscriptionRepository>(stores.Subscriptions); services.AddSingleton<IOnboardingConfirmationRepository>(stores.Confirmations); services.AddSingleton<ITripRepository>(stores.Trips); services.AddSingleton<IOfflineIngestionRepository>(stores.Offline); services.AddSingleton<IIncidentRepository>(stores.Incidents); }); });
    private sealed class TestStores { public Users Users { get; } = new(); public RefreshTokens RefreshTokens { get; } = new(); public Profiles Profiles { get; } = new(); public Vehicles Vehicles { get; } = new(); public Contacts Contacts { get; } = new(); public Codes Codes { get; } = new(); public Devices Devices { get; } = new(); public Subscriptions Subscriptions { get; } = new(); public Confirmations Confirmations { get; } = new(); public Trips Trips { get; } = new(); public Offline Offline { get; } = new(); public Incidents Incidents { get; } = new(); }
    private sealed record LoginLoginEnvelope(bool Success, LoginResponse Data); private sealed record IncidentEnvelope(bool Success, CreateIncidentResponse Data);
    private sealed class Users : IUserRepository { public List<User> Items { get; } = []; public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Id == id)); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase))); public Task AddAsync(User user, CancellationToken ct) { Items.Add(user); return Task.CompletedTask; } public Task UpdateAsync(User user, CancellationToken ct) => Task.CompletedTask; }
    private sealed class RefreshTokens : IRefreshTokenRepository { public List<RefreshToken> Tokens { get; } = []; public Task<RefreshToken?> GetByHashAsync(string h, CancellationToken ct) => Task.FromResult(Tokens.FirstOrDefault(t => t.TokenHash == h)); public Task AddAsync(RefreshToken t, CancellationToken ct) { Tokens.Add(t); return Task.CompletedTask; } public Task UpdateAsync(RefreshToken t, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Profiles : IDriverProfileRepository { public List<DriverProfile> Items { get; } = []; public Task<DriverProfile?> GetByUserIdAsync(string u, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(p => p.UserId == u)); public Task AddAsync(DriverProfile p, CancellationToken ct) { Items.Add(p); return Task.CompletedTask; } public Task UpdateAsync(DriverProfile p, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Vehicles : IDriverVehicleRepository { public List<DriverVehicle> Items { get; } = []; public Task<IReadOnlyList<DriverVehicle>> GetActiveByUserIdAsync(string u, CancellationToken ct) => Task.FromResult<IReadOnlyList<DriverVehicle>>(Items.Where(v => v.UserId == u && v.IsActive).ToArray()); public Task<DriverVehicle?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(v => v.Id == id)); public Task<int> CountActiveByUserIdAsync(string u, CancellationToken ct) => Task.FromResult(0); public Task AddAsync(DriverVehicle v, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(DriverVehicle v, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Contacts : IEmergencyContactRepository { public List<EmergencyContact> Items { get; } = []; public Task<IReadOnlyList<EmergencyContact>> GetActiveByUserIdAsync(string u, CancellationToken ct) => Task.FromResult<IReadOnlyList<EmergencyContact>>(Items.Where(c => c.UserId == u && c.IsActive).ToArray()); public Task<EmergencyContact?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult<EmergencyContact?>(null); public Task<EmergencyContact?> GetByLinkingCodeAsync(string code, CancellationToken ct) => Task.FromResult<EmergencyContact?>(null); public Task<int> CountActiveByUserIdAsync(string u, CancellationToken ct) => Task.FromResult(0); public Task AddAsync(EmergencyContact c, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(EmergencyContact c, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Codes : IDeviceActivationCodeRepository { public Task<IReadOnlyList<DeviceActivationCode>> GetActiveByUserIdAsync(string u, DateTimeOffset n, CancellationToken ct) => Task.FromResult<IReadOnlyList<DeviceActivationCode>>([]); public Task<DeviceActivationCode?> GetActiveCurrentByUserIdAsync(string u, DateTimeOffset n, CancellationToken ct) => Task.FromResult<DeviceActivationCode?>(null); public Task<DeviceActivationCode?> GetByCodeAsync(string c, CancellationToken ct) => Task.FromResult<DeviceActivationCode?>(null); public Task AddAsync(DeviceActivationCode c, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(DeviceActivationCode c, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Devices : IUserDeviceRepository { public List<UserDevice> Items { get; } = []; public Task<IReadOnlyList<UserDevice>> GetActiveByUserIdAsync(string u, CancellationToken ct) => Task.FromResult<IReadOnlyList<UserDevice>>(Items.Where(d => d.UserId == u && d.IsActive).ToArray()); public Task<IReadOnlyList<UserDevice>> GetActiveByParentDeviceIdAsync(string p, CancellationToken ct) => Task.FromResult<IReadOnlyList<UserDevice>>([]); public Task<UserDevice?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(d => d.Id == id)); public Task<UserDevice?> GetByDeviceIdentifierHashAsync(string u, string h, DeviceType t, CancellationToken ct) => Task.FromResult<UserDevice?>(null); public Task<int> CountActiveLinkedByUserIdAndTypeAsync(string u, DeviceType t, CancellationToken ct) => Task.FromResult(0); public Task<bool> HasActiveLinkedMobileAppAsync(string u, CancellationToken ct) => Task.FromResult(Items.Any(d => d.UserId == u && d.DeviceType == DeviceType.MobileApp && d.IsActive && d.LinkStatus == DeviceLinkStatus.Linked)); public Task AddAsync(UserDevice d, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(UserDevice d, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Subscriptions : IUserSubscriptionRepository { public List<UserSubscription> Items { get; } = []; public Task<UserSubscription?> GetByUserIdAsync(string u, CancellationToken ct) => Task.FromResult<UserSubscription?>(null); public Task<bool> HasActiveSubscriptionAsync(string u, CancellationToken ct) => Task.FromResult(Items.Any(s => s.UserId == u && s.Status == SubscriptionStatus.Active)); public Task AddAsync(UserSubscription s, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(UserSubscription s, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Confirmations : IOnboardingConfirmationRepository { public List<OnboardingConfirmation> Items { get; } = []; public Task<OnboardingConfirmation?> GetByUserIdAsync(string u, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(c => c.UserId == u)); public Task AddAsync(OnboardingConfirmation c, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(OnboardingConfirmation c, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Trips : ITripRepository { public List<Trip> Items { get; } = []; public Task<Trip?> GetActiveByUserIdAsync(string u, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.UserId == u && t.Status == TripStatus.Active)); public Task<Trip?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.Id == id)); public Task<IReadOnlyList<Trip>> ListByUserIdAsync(string u, TripStatus? s, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<Trip>>([]); public Task<long> CountByUserIdAsync(string u, TripStatus? s, CancellationToken ct) => Task.FromResult(0L); public Task AddAsync(Trip t, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(Trip t, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Offline : IOfflineIngestionRepository { public Task<OfflineIngestionRecord?> GetByIdempotencyKeyAsync(string k, CancellationToken ct) => Task.FromResult<OfflineIngestionRecord?>(null); public Task<(OfflineIngestionRecord Record, bool IsDuplicate)> AddOrGetDuplicateAsync(OfflineIngestionRecord r, CancellationToken ct) => Task.FromResult((r, false)); }
    private sealed class Incidents : IIncidentRepository { public List<Incident> Items { get; } = []; public Task<Incident?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id)); public Task<Incident?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.IdempotencyKey == key)); public Task<(Incident Incident, bool IsDuplicate)> AddOrGetDuplicateAsync(Incident incident, CancellationToken ct) { Incident? existing = Items.FirstOrDefault(i => i.IdempotencyKey == incident.IdempotencyKey); if (existing is not null) return Task.FromResult((existing, true)); Items.Add(incident); return Task.FromResult((incident, false)); } public Task<IReadOnlyList<Incident>> ListByUserIdAsync(string u, IncidentStatus? s, string? t, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<Incident>>(Items.Where(i => i.UserId == u).ToArray()); public Task<long> CountByUserIdAsync(string u, IncidentStatus? s, string? t, CancellationToken ct) => Task.FromResult((long)Items.Count(i => i.UserId == u)); public Task UpdateAsync(Incident i, CancellationToken ct) => Task.CompletedTask; }
}
