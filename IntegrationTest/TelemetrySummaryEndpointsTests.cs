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
using MotoSOS.API.Modules.MinorEvents.Application;
using MotoSOS.API.Modules.MinorEvents.Domain;
using MotoSOS.API.Modules.TelemetrySummary.Application;
using MotoSOS.API.Modules.TelemetrySummary.Domain;
using MotoSOS.API.Modules.Trips.Application;
using MotoSOS.API.Modules.Trips.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace IntegrationTest;

public sealed class TelemetrySummaryEndpointsTests
{
    [Fact]
    public async Task RiderEndpointsRequireAuthentication()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory(new Stores());
        HttpClient client = factory.CreateClient();
        (await client.GetAsync("/api/v1/rider/trips/trip/telemetry-summary")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsync("/api/v1/rider/trips/trip/telemetry-summary/recompute", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RiderOwnSummaryRecomputeIsIdempotentAndDoesNotExposeRouteOrEvents()
    {
        var stores = new Stores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient rider = factory.CreateClient(); User user = await AuthenticateAsync(rider, "telemetry-rider@example.com", UserRole.Rider, stores); SeedTripAndEvent(stores, user.Id);

        string first = await (await rider.PostAsync("/api/v1/rider/trips/trip/telemetry-summary/recompute", null)).Content.ReadAsStringAsync();
        string second = await (await rider.GetAsync("/api/v1/rider/trips/trip/telemetry-summary")).Content.ReadAsStringAsync();

        first.Should().Contain("Computed").And.Contain("totalMinorEvents").And.NotContain("latitude").And.NotContain("longitude").And.NotContain("polyline").And.NotContain("metadata").And.NotContain("message");
        second.Should().Contain("Computed").And.NotContain("minorEvents");
        stores.Summaries.Items.Should().ContainSingle();
        stores.Trips.Items.Single().Status.Should().Be(TripStatus.Active);
        stores.Events.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task ForeignTripIsNotFoundAndMonitorForbidden()
    {
        var stores = new Stores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient rider = factory.CreateClient(); HttpClient monitor = factory.CreateClient(); User owner = await AuthenticateAsync(rider, "telemetry-owner@example.com", UserRole.Rider, stores); await AuthenticateAsync(monitor, "telemetry-monitor@example.com", UserRole.Monitor, stores); SeedTripAndEvent(stores, owner.Id);

        HttpClient other = factory.CreateClient(); await AuthenticateAsync(other, "telemetry-other@example.com", UserRole.Rider, stores);

        (await other.GetAsync("/api/v1/rider/trips/trip/telemetry-summary")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await monitor.GetAsync("/api/v1/rider/trips/trip/telemetry-summary")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminCanListAndGetByIdButDoesNotRecompute()
    {
        var stores = new Stores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient rider = factory.CreateClient(); HttpClient admin = factory.CreateClient(); HttpClient monitor = factory.CreateClient(); User user = await AuthenticateAsync(rider, "telemetry-rider2@example.com", UserRole.Rider, stores); await AuthenticateAsync(admin, "telemetry-admin@example.com", UserRole.Admin, stores); await AuthenticateAsync(monitor, "telemetry-monitor2@example.com", UserRole.Monitor, stores); SeedTripAndEvent(stores, user.Id); await rider.PostAsync("/api/v1/rider/trips/trip/telemetry-summary/recompute", null); string id = stores.Summaries.Items.Single().Id;

        (await admin.GetAsync("/api/v1/admin/telemetry-summaries")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync($"/api/v1/admin/telemetry-summaries/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync("/api/v1/admin/telemetry-summaries/missing")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await monitor.GetAsync("/api/v1/admin/telemetry-summaries")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.GetAsync("/api/v1/admin/telemetry-summaries?pageSize=101")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.GetAsync("/api/v1/admin/telemetry-summaries?dateFrom=2026-08-10T00:00:00Z&dateTo=2026-08-09T00:00:00Z")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        stores.Summaries.Items.Should().ContainSingle();
    }

    private static void SeedTripAndEvent(Stores stores, string userId)
    {
        stores.Trips.Items.Add(new Trip { Id = "trip", UserId = userId, VehicleId = "vehicle", MobileDeviceId = "mobile", Status = TripStatus.Active, CreatedAtUtc = DateTimeOffset.UtcNow });
        stores.Events.Items.Add(new MinorEvent { Id = "event", UserId = userId, TripId = "trip", VehicleId = "vehicle", EventType = MinorEventType.HardBrake, Severity = MinorEventSeverity.Medium, Source = MinorEventSource.MobileApp, Status = MinorEventStatus.Recorded, Confidence = .8, Score = 90, BatteryLevel = 30, SpeedKmh = 50, GpsQuality = "Good", Latitude = 19, Longitude = -99, Message = "hidden", OccurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1), CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1) });
    }

    private static async Task<User> AuthenticateAsync(HttpClient client, string email, UserRole role, Stores stores) { var register = new RegisterRequest(email, "StrongPass1!", "StrongPass1!", "Moto Rider", "+52 555", "Rider", true); await client.PostAsJsonAsync("/api/v1/auth/register", register); User user = stores.Users.Items.Single(u => u.Email == email); user.Role = role; LoginEnvelope login = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, register.Password))).Content.ReadFromJsonAsync<LoginEnvelope>())!; client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Data.AccessToken); return user; }
    private static WebApplicationFactory<Program> CreateFactory(Stores stores) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Issuer"] = "MotoSOS", ["Jwt:Audience"] = "MotoSOS.Clients", ["Jwt:Key"] = new string('T', 48), ["Jwt:AccessTokenMinutes"] = "15", ["Jwt:RefreshTokenDays"] = "7", ["Jwt:RefreshTokenRememberMeDays"] = "30", ["MongoDb:" + "Connection" + "String"] = string.Empty, ["MongoDb:DatabaseName"] = "MotoSOS_Test" })); builder.ConfigureTestServices(services => { services.AddSingleton<IUserRepository>(stores.Users); services.AddSingleton<IRefreshTokenRepository>(stores.RefreshTokens); services.AddSingleton<ITripRepository>(stores.Trips); services.AddSingleton<IMinorEventRepository>(stores.Events); services.AddSingleton<ITelemetrySummaryRepository>(stores.Summaries); }); });
    private sealed record LoginEnvelope(bool Success, LoginResponse Data);
    private sealed class Stores { public Users Users { get; } = new(); public RefreshTokens RefreshTokens { get; } = new(); public Trips Trips { get; } = new(); public Events Events { get; } = new(); public Summaries Summaries { get; } = new(); }
    private sealed class Users : IUserRepository { public List<User> Items { get; } = []; public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Id == id)); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Email == email)); public Task AddAsync(User u, CancellationToken ct) { Items.Add(u); return Task.CompletedTask; } public Task UpdateAsync(User u, CancellationToken ct) => Task.CompletedTask; }
    private sealed class RefreshTokens : IRefreshTokenRepository { public List<RefreshToken> Items { get; } = []; public Task<RefreshToken?> GetByHashAsync(string h, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.TokenHash == h)); public Task AddAsync(RefreshToken t, CancellationToken ct) { Items.Add(t); return Task.CompletedTask; } public Task UpdateAsync(RefreshToken t, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Trips : ITripRepository { public List<Trip> Items { get; } = []; public Task<Trip?> GetActiveByUserIdAsync(string u, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.UserId == u && t.Status == TripStatus.Active)); public Task<Trip?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.Id == id)); public Task<IReadOnlyList<Trip>> ListByUserIdAsync(string u, TripStatus? s, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<Trip>>([]); public Task<long> CountByUserIdAsync(string u, TripStatus? s, CancellationToken ct) => Task.FromResult(0L); public Task AddAsync(Trip t, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(Trip t, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Events : IMinorEventRepository { public List<MinorEvent> Items { get; } = []; public Task<MinorEvent?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(e => e.Id == id)); public Task<MinorEvent?> GetByIdempotencyKeyAsync(string k, CancellationToken ct) => Task.FromResult<MinorEvent?>(null); public Task<(MinorEvent MinorEvent, bool IsDuplicate)> AddOrGetDuplicateAsync(MinorEvent e, CancellationToken ct) => Task.FromResult((e, false)); public Task UpdateAsync(MinorEvent e, CancellationToken ct) => Task.CompletedTask; public Task<IReadOnlyList<MinorEvent>> ListByUserIdAsync(string u, MinorEventQuery q, CancellationToken ct) => Task.FromResult<IReadOnlyList<MinorEvent>>([]); public Task<IReadOnlyList<MinorEvent>> ListByTripIdAsync(string u, string t, CancellationToken ct) => Task.FromResult<IReadOnlyList<MinorEvent>>(Items.Where(e => e.UserId == u && e.TripId == t).ToArray()); public Task<long> CountByUserIdAsync(string u, MinorEventQuery q, CancellationToken ct) => Task.FromResult(0L); public Task<IReadOnlyList<MinorEvent>> ListAsync(MinorEventQuery q, CancellationToken ct) => Task.FromResult<IReadOnlyList<MinorEvent>>([]); public Task<long> CountAsync(MinorEventQuery q, CancellationToken ct) => Task.FromResult(0L); }
    private sealed class Summaries : ITelemetrySummaryRepository { public List<TripTelemetrySummary> Items { get; } = []; public Task<TripTelemetrySummary?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(s => s.Id == id)); public Task<TripTelemetrySummary?> GetByTripIdAsync(string tripId, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(s => s.TripId == tripId)); public Task<TripTelemetrySummary?> GetByUserIdAndTripIdAsync(string userId, string tripId, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(s => s.UserId == userId && s.TripId == tripId)); public Task<TripTelemetrySummary> UpsertAsync(TripTelemetrySummary s, CancellationToken ct) { TripTelemetrySummary? existing = Items.FirstOrDefault(x => x.UserId == s.UserId && x.TripId == s.TripId); if (existing is not null) { s.Id = existing.Id; s.CreatedAtUtc = existing.CreatedAtUtc; Items.Remove(existing); } Items.Add(s); return Task.FromResult(s); } public Task<IReadOnlyList<TripTelemetrySummary>> ListByUserIdAsync(string userId, TelemetrySummaryQuery q, CancellationToken ct) => Task.FromResult<IReadOnlyList<TripTelemetrySummary>>(Items.Where(s => s.UserId == userId).ToArray()); public Task<long> CountByUserIdAsync(string userId, TelemetrySummaryQuery q, CancellationToken ct) => Task.FromResult((long)Items.Count(s => s.UserId == userId)); public Task<IReadOnlyList<TripTelemetrySummary>> ListAsync(TelemetrySummaryQuery q, CancellationToken ct) => Task.FromResult<IReadOnlyList<TripTelemetrySummary>>(Items); public Task<long> CountAsync(TelemetrySummaryQuery q, CancellationToken ct) => Task.FromResult((long)Items.Count); }
}
