using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MotoSOS.API.Modules.AuditLogs.Application;
using MotoSOS.API.Modules.AuditLogs.Contracts;
using MotoSOS.API.Modules.AuditLogs.Domain;
using MotoSOS.API.Modules.Auth.Application;
using MotoSOS.API.Modules.Auth.Contracts;
using MotoSOS.API.Modules.Auth.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace IntegrationTest;

public sealed class AuditLogEndpointsTests
{
    [Fact]
    public async Task AuditLogEndpointsRequireAuthentication()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory(new Stores());
        HttpClient client = factory.CreateClient();
        (await client.GetAsync("/api/v1/admin/audit-logs")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/admin/audit-logs/log-1")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RiderAndMonitorAreForbiddenAndAdminCanListAndGetById()
    {
        var stores = new Stores();
        stores.AuditLogs.Items.Add(Entry("log-1"));
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient admin = factory.CreateClient(); HttpClient rider = factory.CreateClient(); HttpClient monitor = factory.CreateClient();
        await AuthenticateAsync(admin, "audit-admin@example.com", UserRole.Admin, stores);
        await AuthenticateAsync(rider, "audit-rider@example.com", UserRole.Rider, stores);
        await AuthenticateAsync(monitor, "audit-monitor@example.com", UserRole.Monitor, stores);

        (await rider.GetAsync("/api/v1/admin/audit-logs")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await monitor.GetAsync("/api/v1/admin/audit-logs/log-1")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        GetEnvelope list = (await (await admin.GetAsync("/api/v1/admin/audit-logs")).Content.ReadFromJsonAsync<GetEnvelope>())!;
        list.Data.AuditLogs.Should().ContainSingle(log => log.Id == "log-1");
        AuditEnvelope single = (await (await admin.GetAsync("/api/v1/admin/audit-logs/log-1")).Content.ReadFromJsonAsync<AuditEnvelope>())!;
        single.Data.Id.Should().Be("log-1");
    }

    [Fact]
    public async Task InvalidQueriesAndMissingIdReturnControlledErrors()
    {
        var stores = new Stores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient admin = factory.CreateClient(); await AuthenticateAsync(admin, "audit-admin2@example.com", UserRole.Admin, stores);

        (await admin.GetAsync("/api/v1/admin/audit-logs?dateFrom=2026-08-03T00:00:00Z&dateTo=2026-08-02T00:00:00Z")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.GetAsync("/api/v1/admin/audit-logs?action=bad")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.GetAsync("/api/v1/admin/audit-logs?pageSize=101")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.GetAsync("/api/v1/admin/audit-logs/missing")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task FiltersWorkResponseIsSanitizedAndQueriesAreNotAudited()
    {
        var stores = new Stores();
        stores.AuditLogs.Items.Add(Entry("log-1", metadata: new Dictionary<string, string> { ["processed"] = "1", ["refreshToken"] = "secret", ["phone"] = "+5255" }));
        stores.AuditLogs.Items.Add(Entry("log-2", action: AuditAction.AuthLogin));
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient admin = factory.CreateClient(); await AuthenticateAsync(admin, "audit-admin3@example.com", UserRole.Admin, stores);
        int before = stores.AuditLogs.Items.Count;

        string body = await (await admin.GetAsync("/api/v1/admin/audit-logs?action=IncidentClosed&module=Incidents&outcome=Success&entityType=Incident&entityId=incident-1")).Content.ReadAsStringAsync();

        body.Should().Contain("log-1").And.NotContain("log-2").And.NotContain("refreshToken").And.NotContain("+5255").And.NotContain("secret");
        stores.AuditLogs.Items.Count.Should().Be(before);
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);
    private static AuditLogEntry Entry(string id, AuditAction action = AuditAction.IncidentClosed, IReadOnlyDictionary<string, string>? metadata = null) => new() { Id = id, ActorUserId = "actor", ActorRole = "Rider", Action = action, Module = action == AuditAction.AuthLogin ? AuditModule.Auth : AuditModule.Incidents, Outcome = AuditOutcome.Success, EntityType = action == AuditAction.AuthLogin ? "User" : "Incident", EntityId = action == AuditAction.AuthLogin ? "actor" : "incident-1", CreatedAtUtc = Now, Metadata = metadata?.ToDictionary(k => k.Key, v => v.Value) ?? [] };
    private static async Task<User> AuthenticateAsync(HttpClient client, string email, UserRole role, Stores stores) { var register = new RegisterRequest(email, "StrongPass1!", "StrongPass1!", "Moto User", null, role == UserRole.Monitor ? "Monitor" : "Rider", true); await client.PostAsJsonAsync("/api/v1/auth/register", register); User user = stores.Users.Items.Single(u => u.Email == email); user.Role = role; LoginEnvelope login = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, register.Password))).Content.ReadFromJsonAsync<LoginEnvelope>())!; client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Data.AccessToken); return user; }
    private static WebApplicationFactory<Program> CreateFactory(Stores stores) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Issuer"] = "MotoSOS", ["Jwt:Audience"] = "MotoSOS.Clients", ["Jwt:Key"] = new string('A', 48), ["Jwt:AccessTokenMinutes"] = "15", ["Jwt:RefreshTokenDays"] = "7", ["Jwt:RefreshTokenRememberMeDays"] = "30", ["MongoDb:" + "Connection" + "String"] = string.Empty, ["MongoDb:DatabaseName"] = "MotoSOS_Test" })); builder.ConfigureTestServices(services => { services.AddSingleton<IUserRepository>(stores.Users); services.AddSingleton<IRefreshTokenRepository>(stores.RefreshTokens); services.AddSingleton<IAuditLogRepository>(stores.AuditLogs); }); });
    private sealed record LoginEnvelope(bool Success, LoginResponse Data);
    private sealed record GetEnvelope(bool Success, GetAuditLogsResponse Data);
    private sealed record AuditEnvelope(bool Success, AuditLogResponse Data);
    private sealed class Stores { public Users Users { get; } = new(); public RefreshTokens RefreshTokens { get; } = new(); public AuditLogs AuditLogs { get; } = new(); }
    private sealed class Users : IUserRepository { public List<User> Items { get; } = []; public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Id == id)); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Email == email)); public Task AddAsync(User u, CancellationToken ct) { Items.Add(u); return Task.CompletedTask; } public Task UpdateAsync(User u, CancellationToken ct) => Task.CompletedTask; }
    private sealed class RefreshTokens : IRefreshTokenRepository { public List<RefreshToken> Items { get; } = []; public Task<RefreshToken?> GetByHashAsync(string h, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.TokenHash == h)); public Task AddAsync(RefreshToken t, CancellationToken ct) { Items.Add(t); return Task.CompletedTask; } public Task UpdateAsync(RefreshToken t, CancellationToken ct) => Task.CompletedTask; }
    private sealed class AuditLogs : IAuditLogRepository { public List<AuditLogEntry> Items { get; } = []; public Task AddAsync(AuditLogEntry entry, CancellationToken ct) { Items.Add(entry); return Task.CompletedTask; } public Task<AuditLogEntry?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id)); public Task<IReadOnlyList<AuditLogEntry>> ListAsync(AuditLogQuery q, CancellationToken ct) => Task.FromResult<IReadOnlyList<AuditLogEntry>>(Apply(q).OrderByDescending(i => i.CreatedAtUtc).Skip((q.PageNumber - 1) * q.PageSize).Take(q.PageSize).ToArray()); public Task<long> CountAsync(AuditLogQuery q, CancellationToken ct) => Task.FromResult((long)Apply(q).Count()); private IEnumerable<AuditLogEntry> Apply(AuditLogQuery q) => Items.Where(i => (q.ActorUserId is null || i.ActorUserId == q.ActorUserId) && (!q.Action.HasValue || i.Action == q.Action) && (!q.Module.HasValue || i.Module == q.Module) && (!q.Outcome.HasValue || i.Outcome == q.Outcome) && (q.EntityType is null || i.EntityType == q.EntityType) && (q.EntityId is null || i.EntityId == q.EntityId)); }
}
