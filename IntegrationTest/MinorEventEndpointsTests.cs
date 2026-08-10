using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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
using MotoSOS.API.Modules.MinorEvents.Application;
using MotoSOS.API.Modules.MinorEvents.Contracts;
using MotoSOS.API.Modules.MinorEvents.Domain;
using MotoSOS.API.Modules.Trips.Application;
using MotoSOS.API.Modules.Trips.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace IntegrationTest;

public sealed class MinorEventEndpointsTests
{
    [Fact]
    public async Task MinorEventEndpointsRequireAuthentication()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory(new Stores()); HttpClient client = factory.CreateClient();
        (await client.PostAsJsonAsync("/api/v1/mobile/minor-events", Request("trip", "mobile"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/rider/minor-events")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/admin/minor-events")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RiderCreatesListsGetsReviewsAndIgnoresMinorEventsIdempotently()
    {
        var stores = new Stores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient client = factory.CreateClient(); User rider = await AuthenticateAsync(client, "minor-rider@example.com", "Rider", stores); (Trip trip, UserDevice mobile) = SeedTrip(stores, rider.Id);
        MinorEventEnvelope first = (await (await client.PostAsJsonAsync("/api/v1/mobile/minor-events", Request(trip.Id, mobile.Id))).Content.ReadFromJsonAsync<MinorEventEnvelope>())!;
        MinorEventEnvelope second = (await (await client.PostAsJsonAsync("/api/v1/mobile/minor-events", Request(trip.Id, mobile.Id))).Content.ReadFromJsonAsync<MinorEventEnvelope>())!;
        string list = await (await client.GetAsync($"/api/v1/rider/minor-events?tripId={trip.Id}&eventType=HardBrake")).Content.ReadAsStringAsync();
        HttpResponseMessage get = await client.GetAsync($"/api/v1/rider/minor-events/{first.Data.MinorEvent.Id}");
        MinorEventEnvelope reviewed = (await (await client.PostAsync($"/api/v1/rider/minor-events/{first.Data.MinorEvent.Id}/mark-reviewed", null)).Content.ReadFromJsonAsync<MinorEventEnvelope>())!;
        MinorEventEnvelope ignored = (await (await client.PostAsync($"/api/v1/rider/minor-events/{first.Data.MinorEvent.Id}/ignore", null)).Content.ReadFromJsonAsync<MinorEventEnvelope>())!;

        first.Data.MinorEvent.Id.Should().Be(second.Data.MinorEvent.Id);
        stores.Events.Items.Should().ContainSingle();
        list.Should().Contain(first.Data.MinorEvent.Id).And.NotContain("access" + "Token");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        reviewed.Data.MinorEvent.Status.Should().Be("Reviewed");
        ignored.Data.MinorEvent.Status.Should().Be("Ignored");
    }

    [Theory]
    [InlineData("Monitor")]
    [InlineData("Admin")]
    public async Task NonRidersCannotCreateMinorEvents(string role)
    {
        var stores = new Stores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient client = factory.CreateClient(); User user = await AuthenticateAsync(client, $"minor-{role}@example.com", role, stores); (Trip trip, UserDevice mobile) = SeedTrip(stores, user.Id);
        (await client.PostAsJsonAsync("/api/v1/mobile/minor-events", Request(trip.Id, mobile.Id))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RiderCannotCreateOrReadOtherUsersMinorEvents()
    {
        var stores = new Stores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient owner = factory.CreateClient(); HttpClient otherClient = factory.CreateClient(); User ownerUser = await AuthenticateAsync(owner, "minor-owner@example.com", "Rider", stores); User otherUser = await AuthenticateAsync(otherClient, "minor-other@example.com", "Rider", stores); (Trip trip, UserDevice mobile) = SeedTrip(stores, ownerUser.Id);
        MinorEventEnvelope created = (await (await owner.PostAsJsonAsync("/api/v1/mobile/minor-events", Request(trip.Id, mobile.Id))).Content.ReadFromJsonAsync<MinorEventEnvelope>())!;
        (await otherClient.PostAsJsonAsync("/api/v1/mobile/minor-events", Request(trip.Id, mobile.Id))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await otherClient.GetAsync($"/api/v1/rider/minor-events/{created.Data.MinorEvent.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        string otherList = await (await otherClient.GetAsync("/api/v1/rider/minor-events")).Content.ReadAsStringAsync();
        otherList.Should().NotContain(created.Data.MinorEvent.Id);
        otherUser.Id.Should().NotBe(ownerUser.Id);
    }

    [Fact]
    public async Task AdminCanListWithFiltersAndRiderMonitorAreForbidden()
    {
        var stores = new Stores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient rider = factory.CreateClient(); HttpClient admin = factory.CreateClient(); HttpClient monitor = factory.CreateClient(); User riderUser = await AuthenticateAsync(rider, "minor-list-rider@example.com", "Rider", stores); await AuthenticateAsync(admin, "minor-admin@example.com", "Admin", stores); await AuthenticateAsync(monitor, "minor-monitor@example.com", "Monitor", stores); (Trip trip, UserDevice mobile) = SeedTrip(stores, riderUser.Id);
        MinorEventEnvelope created = (await (await rider.PostAsJsonAsync("/api/v1/mobile/minor-events", Request(trip.Id, mobile.Id))).Content.ReadFromJsonAsync<MinorEventEnvelope>())!;
        string body = await (await admin.GetAsync($"/api/v1/admin/minor-events?userId={riderUser.Id}&status=Recorded&pageSize=20")).Content.ReadAsStringAsync();
        body.Should().Contain(created.Data.MinorEvent.Id);
        (await rider.GetAsync("/api/v1/admin/minor-events")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await monitor.GetAsync("/api/v1/admin/minor-events")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 8, 18, 30, 0, TimeSpan.Zero);
    private static CreateMinorEventRequest Request(string tripId, string mobileId) => new(tripId, "mobile-event-001", "HardBrake", "Low", "MobileApp", mobileId, null, 42.5, 0.72, "Good", 19.2826, -99.6557, 45.2, 82, "Hard brake detected.", Now, new Dictionary<string, string> { ["sensor"] = "accelerometer", ["access" + "Token"] = "hidden" });
    private static (Trip Trip, UserDevice Mobile) SeedTrip(Stores stores, string userId) { var mobile = new UserDevice { Id = Guid.NewGuid().ToString("N"), UserId = userId, DeviceType = DeviceType.MobileApp, DeviceName = "Phone", IsActive = true, LinkStatus = DeviceLinkStatus.Linked }; var trip = new Trip { Id = Guid.NewGuid().ToString("N"), UserId = userId, VehicleId = "vehicle", MobileDeviceId = mobile.Id, Status = TripStatus.Active, StartedAtUtc = Now, CreatedAtUtc = Now }; stores.Devices.Items.Add(mobile); stores.Trips.Items.Add(trip); return (trip, mobile); }
    private static async Task<User> AuthenticateAsync(HttpClient client, string email, string role, Stores stores) { string pass = "StrongPass1!"; var register = new RegisterRequest(email, pass, pass, "Moto Rider", "+52 555 555 5555", "Rider", true); await client.PostAsJsonAsync("/api/v1/auth/register", register); User user = stores.Users.Items.Single(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)); if (role == "Monitor") user.Role = UserRole.Monitor; if (role == "Admin") user.Role = UserRole.Admin; LoginEnvelope login = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, pass))).Content.ReadFromJsonAsync<LoginEnvelope>())!; client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Data.AccessToken); return user; }
    private static WebApplicationFactory<Program> CreateFactory(Stores stores) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Issuer"] = "MotoSOS", ["Jwt:Audience"] = "MotoSOS.Clients", ["Jwt:Key"] = new string('M', 48), ["Jwt:AccessTokenMinutes"] = "15", ["Jwt:RefreshTokenDays"] = "7", ["Jwt:RefreshTokenRememberMeDays"] = "30", ["MongoDb:" + "Connection" + "String"] = string.Empty, ["MongoDb:DatabaseName"] = "MotoSOS_Test" })); builder.ConfigureTestServices(services => { services.AddSingleton<IUserRepository>(stores.Users); services.AddSingleton<IRefreshTokenRepository>(stores.RefreshTokens); services.AddSingleton<ITripRepository>(stores.Trips); services.AddSingleton<IUserDeviceRepository>(stores.Devices); services.AddSingleton<IMinorEventRepository>(stores.Events); }); });
    private sealed record LoginEnvelope(bool Success, LoginResponse Data);
    private sealed record MinorEventEnvelope(bool Success, GetMinorEventData Data);
    private sealed record GetMinorEventData(MinorEventResponse MinorEvent);
    private sealed class Stores { public Users Users { get; } = new(); public RefreshTokens RefreshTokens { get; } = new(); public Trips Trips { get; } = new(); public Devices Devices { get; } = new(); public Events Events { get; } = new(); }
    private sealed class Users : IUserRepository { public List<User> Items { get; } = []; public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Id == id)); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Email == email)); public Task AddAsync(User user, CancellationToken ct) { Items.Add(user); return Task.CompletedTask; } public Task UpdateAsync(User user, CancellationToken ct) => Task.CompletedTask; }
    private sealed class RefreshTokens : IRefreshTokenRepository { public List<RefreshToken> Items { get; } = []; public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.TokenHash == tokenHash)); public Task AddAsync(RefreshToken token, CancellationToken ct) { Items.Add(token); return Task.CompletedTask; } public Task UpdateAsync(RefreshToken token, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Trips : ITripRepository { public List<Trip> Items { get; } = []; public Task<Trip?> GetActiveByUserIdAsync(string userId, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.UserId == userId && t.Status == TripStatus.Active)); public Task<Trip?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.Id == id)); public Task<IReadOnlyList<Trip>> ListByUserIdAsync(string userId, TripStatus? status, int pageNumber, int pageSize, CancellationToken ct) => Task.FromResult<IReadOnlyList<Trip>>([]); public Task<long> CountByUserIdAsync(string userId, TripStatus? status, CancellationToken ct) => Task.FromResult(0L); public Task AddAsync(Trip trip, CancellationToken ct) { Items.Add(trip); return Task.CompletedTask; } public Task UpdateAsync(Trip trip, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Devices : IUserDeviceRepository { public List<UserDevice> Items { get; } = []; public Task<IReadOnlyList<UserDevice>> GetActiveByUserIdAsync(string userId, CancellationToken ct) => Task.FromResult<IReadOnlyList<UserDevice>>([]); public Task<IReadOnlyList<UserDevice>> GetActiveByParentDeviceIdAsync(string parentDeviceId, CancellationToken ct) => Task.FromResult<IReadOnlyList<UserDevice>>([]); public Task<UserDevice?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(d => d.Id == id)); public Task<UserDevice?> GetByDeviceIdentifierHashAsync(string userId, string hash, DeviceType deviceType, CancellationToken ct) => Task.FromResult<UserDevice?>(null); public Task<int> CountActiveLinkedByUserIdAndTypeAsync(string userId, DeviceType deviceType, CancellationToken ct) => Task.FromResult(0); public Task<bool> HasActiveLinkedMobileAppAsync(string userId, CancellationToken ct) => Task.FromResult(true); public Task AddAsync(UserDevice device, CancellationToken ct) { Items.Add(device); return Task.CompletedTask; } public Task UpdateAsync(UserDevice device, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Events : IMinorEventRepository { public List<MinorEvent> Items { get; } = []; public Task<MinorEvent?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(e => e.Id == id)); public Task<MinorEvent?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(e => e.IdempotencyKey == key)); public Task<(MinorEvent MinorEvent, bool IsDuplicate)> AddOrGetDuplicateAsync(MinorEvent e, CancellationToken ct) { MinorEvent? existing = Items.FirstOrDefault(i => i.IdempotencyKey == e.IdempotencyKey); if (existing is not null) return Task.FromResult((existing, true)); Items.Add(e); return Task.FromResult((e, false)); } public Task UpdateAsync(MinorEvent minorEvent, CancellationToken ct) => Task.CompletedTask; public Task<IReadOnlyList<MinorEvent>> ListByUserIdAsync(string userId, MinorEventQuery q, CancellationToken ct) => Task.FromResult<IReadOnlyList<MinorEvent>>(Items.Where(e => e.UserId == userId && (q.TripId is null || e.TripId == q.TripId) && (q.EventType is null || e.EventType == q.EventType) && (q.Status is null || e.Status == q.Status)).OrderByDescending(e => e.OccurredAtUtc).ToArray()); public Task<long> CountByUserIdAsync(string userId, MinorEventQuery q, CancellationToken ct) => Task.FromResult((long)Items.Count(e => e.UserId == userId)); public Task<IReadOnlyList<MinorEvent>> ListAsync(MinorEventQuery q, CancellationToken ct) => Task.FromResult<IReadOnlyList<MinorEvent>>(Items.Where(e => (q.UserId is null || e.UserId == q.UserId) && (q.Status is null || e.Status == q.Status)).ToArray()); public Task<long> CountAsync(MinorEventQuery q, CancellationToken ct) => Task.FromResult((long)Items.Count); }
}
