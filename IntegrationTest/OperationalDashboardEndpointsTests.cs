using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MotoSOS.API.Modules.AlertAcknowledgements.Domain;
using MotoSOS.API.Modules.AlertDispatch.Domain;
using MotoSOS.API.Modules.Auth.Application;
using MotoSOS.API.Modules.Auth.Contracts;
using MotoSOS.API.Modules.Auth.Domain;
using MotoSOS.API.Modules.EmergencyResolution.Domain;
using MotoSOS.API.Modules.Incidents.Domain;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.OfflineIngestion.Domain;
using MotoSOS.API.Modules.OperationalDashboard.Application;
using MotoSOS.API.Modules.Trips.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace IntegrationTest;

public sealed class OperationalDashboardEndpointsTests
{
    [Fact]
    public async Task DashboardEndpointsRequireAuthentication()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory(new Stores()); HttpClient client = factory.CreateClient();
        (await client.GetAsync("/api/v1/admin/dashboard/summary")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/admin/dashboard/incidents")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/admin/dashboard/response-times")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/admin/dashboard/resolution-outcomes")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/admin/dashboard/offline-processing")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminCanGetSummaryAndRiderMonitorAreForbidden()
    {
        var stores = new Stores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient admin = factory.CreateClient(); HttpClient rider = factory.CreateClient(); HttpClient monitor = factory.CreateClient(); await AuthenticateAsync(admin, "dash-admin@example.com", UserRole.Admin, stores); await AuthenticateAsync(rider, "dash-rider@example.com", UserRole.Rider, stores); await AuthenticateAsync(monitor, "dash-monitor@example.com", UserRole.Monitor, stores);

        string body = await (await admin.GetAsync("/api/v1/admin/dashboard/summary")).Content.ReadAsStringAsync();

        body.Should().Contain("users").And.Contain("onboarding").And.Contain("trips").And.Contain("incidents").And.Contain("alerts").And.Contain("notifications").And.Contain("acknowledgements").And.Contain("resolutionReports").And.Contain("offlineProcessing");
        (await rider.GetAsync("/api/v1/admin/dashboard/summary")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await monitor.GetAsync("/api/v1/admin/dashboard/summary")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminCanUseDashboardEndpointsAndInvalidQueriesReturnValidationError()
    {
        var stores = new Stores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient admin = factory.CreateClient(); await AuthenticateAsync(admin, "dash-admin2@example.com", UserRole.Admin, stores);

        (await admin.GetAsync("/api/v1/admin/dashboard/incidents?status=Closed&pageNumber=1&pageSize=20")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync("/api/v1/admin/dashboard/response-times")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync("/api/v1/admin/dashboard/resolution-outcomes")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync("/api/v1/admin/dashboard/offline-processing")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync("/api/v1/admin/dashboard/incidents?dateFrom=2026-08-03T00:00:00Z&dateTo=2026-08-02T00:00:00Z")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.GetAsync("/api/v1/admin/dashboard/incidents?pageSize=101")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.GetAsync("/api/v1/admin/dashboard/incidents?status=closed")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.GetAsync("/api/v1/admin/dashboard/response-times?dateFrom=not-a-date")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DashboardResponsesDoNotExposeSensitiveFields()
    {
        var stores = new Stores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient admin = factory.CreateClient(); await AuthenticateAsync(admin, "dash-admin3@example.com", UserRole.Admin, stores);
        string body = await (await admin.GetAsync("/api/v1/admin/dashboard/incidents")).Content.ReadAsStringAsync();
        body.Should().NotContain("userId").And.NotContain("email").And.NotContain("phone").And.NotContain("password" + "Hash").And.NotContain("refresh" + "Token").And.NotContain("access" + "Token").And.NotContain("device" + "Identifier").And.NotContain("payload");
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);
    private static async Task<User> AuthenticateAsync(HttpClient client, string email, UserRole role, Stores stores) { var register = new RegisterRequest(email, "StrongPass1!", "StrongPass1!", "Moto Rider", "+52 555", "Rider", true); await client.PostAsJsonAsync("/api/v1/auth/register", register); User user = stores.Users.Items.Single(u => u.Email == email); user.Role = role; LoginEnvelope login = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, register.Password))).Content.ReadFromJsonAsync<LoginEnvelope>())!; client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Data.AccessToken); return user; }
    private static WebApplicationFactory<Program> CreateFactory(Stores stores) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Issuer"] = "MotoSOS", ["Jwt:Audience"] = "MotoSOS.Clients", ["Jwt:Key"] = new string('D', 48), ["Jwt:AccessTokenMinutes"] = "15", ["Jwt:RefreshTokenDays"] = "7", ["Jwt:RefreshTokenRememberMeDays"] = "30", ["MongoDb:" + "Connection" + "String"] = string.Empty, ["MongoDb:DatabaseName"] = "MotoSOS_Test" })); builder.ConfigureTestServices(services => { services.AddSingleton<IUserRepository>(stores.Users); services.AddSingleton<IRefreshTokenRepository>(stores.RefreshTokens); services.AddSingleton<IOperationalDashboardRepository>(stores.Dashboard); }); });
    private sealed record LoginEnvelope(bool Success, LoginResponse Data);
    private sealed class Stores { public Users Users { get; } = new(); public RefreshTokens RefreshTokens { get; } = new(); public Dashboard Dashboard { get; } = new(); }
    private sealed class Users : IUserRepository { public List<User> Items { get; } = []; public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Id == id)); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Email == email)); public Task AddAsync(User u, CancellationToken ct) { Items.Add(u); return Task.CompletedTask; } public Task UpdateAsync(User u, CancellationToken ct) => Task.CompletedTask; }
    private sealed class RefreshTokens : IRefreshTokenRepository { public List<RefreshToken> Items { get; } = []; public Task<RefreshToken?> GetByHashAsync(string h, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.TokenHash == h)); public Task AddAsync(RefreshToken t, CancellationToken ct) { Items.Add(t); return Task.CompletedTask; } public Task UpdateAsync(RefreshToken t, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Dashboard : IOperationalDashboardRepository
    {
        private readonly Incident[] _incidents = [new() { Id = "incident", TripId = "trip", Status = IncidentStatus.Closed, Source = IncidentSource.MobileDetection, Cause = IncidentCause.CountdownTimeout, RiskLevel = IncidentRiskLevel.High, OccurredAtUtc = Now, CreatedAtUtc = Now, ClosedAtUtc = Now.AddMinutes(5) }];
        private readonly EmergencyResolutionReport[] _reports = [new() { Outcome = EmergencyResolutionOutcome.UserSafe, ResponseTimeSeconds = 60, CreatedAtUtc = Now }, new() { Outcome = EmergencyResolutionOutcome.FalsePositive, CreatedAtUtc = Now }];
        public Task<long> CountUsersAsync(UserRole? r, CancellationToken ct) => Task.FromResult(3L); public Task<long> CountOperationalOnboardingAsync(CancellationToken ct) => Task.FromResult(1L); public Task<long> CountTripsAsync(TripStatus? s, CancellationToken ct) => Task.FromResult(1L); public Task<long> CountIncidentsAsync(IncidentStatus? s, DateTimeOffset? f, DateTimeOffset? t, CancellationToken ct) => Task.FromResult((long)_incidents.Count(i => !s.HasValue || i.Status == s)); public Task<IReadOnlyList<Incident>> ListIncidentsAsync(IncidentStatus? s, DateTimeOffset? f, DateTimeOffset? t, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<Incident>>(_incidents.Where(i => !s.HasValue || i.Status == s).ToArray()); public Task<long> CountAlertDispatchesAsync(AlertDispatchStatus? s, CancellationToken ct) => Task.FromResult(1L); public Task<long> CountNotificationsAsync(NotificationDeliveryStatus? s, CancellationToken ct) => Task.FromResult(1L); public Task<long> CountAcknowledgementsAsync(AlertAcknowledgementStatus? s, CancellationToken ct) => Task.FromResult(1L); public Task<long> CountResolutionReportsAsync(DateTimeOffset? f, DateTimeOffset? t, CancellationToken ct) => Task.FromResult((long)_reports.Length); public Task<IReadOnlyList<EmergencyResolutionReport>> ListResolutionReportsAsync(DateTimeOffset? f, DateTimeOffset? t, CancellationToken ct) => Task.FromResult<IReadOnlyList<EmergencyResolutionReport>>(_reports); public Task<long> CountOfflineRecordsAsync(OfflineIngestionProcessingStatus s, CancellationToken ct) => Task.FromResult(1L);
    }
}
