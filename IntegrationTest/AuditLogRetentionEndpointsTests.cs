using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MotoSOS.API.Modules.AuditLogRetention.Application;
using MotoSOS.API.Modules.AuditLogRetention.Contracts;
using MotoSOS.API.Modules.AuditLogRetention.Domain;
using MotoSOS.API.Modules.AuditLogs.Application;
using MotoSOS.API.Modules.AuditLogs.Domain;
using MotoSOS.API.Modules.Auth.Application;
using MotoSOS.API.Modules.Auth.Contracts;
using MotoSOS.API.Modules.Auth.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace IntegrationTest;

public sealed class AuditLogRetentionEndpointsTests
{
    [Fact]
    public async Task RetentionEndpointsRequireAuthentication()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory(new Stores());
        HttpClient client = factory.CreateClient();

        (await client.GetAsync("/api/v1/admin/audit-logs/retention/policy")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/v1/admin/audit-logs/retention/run", new RunAuditLogRetentionRequest(null, null, null))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/admin/audit-logs/retention/runs")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/admin/audit-logs/retention/runs/run-1")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RiderAndMonitorAreForbiddenAndAdminCanReadPolicy()
    {
        var stores = new Stores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient admin = factory.CreateClient(); HttpClient rider = factory.CreateClient(); HttpClient monitor = factory.CreateClient();
        await AuthenticateAsync(admin, "retention-admin@example.com", UserRole.Admin, stores);
        await AuthenticateAsync(rider, "retention-rider@example.com", UserRole.Rider, stores);
        await AuthenticateAsync(monitor, "retention-monitor@example.com", UserRole.Monitor, stores);

        (await rider.GetAsync("/api/v1/admin/audit-logs/retention/policy")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await monitor.GetAsync("/api/v1/admin/audit-logs/retention/runs")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        PolicyEnvelope policy = (await (await admin.GetAsync("/api/v1/admin/audit-logs/retention/policy")).Content.ReadFromJsonAsync<PolicyEnvelope>())!;
        policy.Data.AutomaticWorkerEnabled.Should().BeFalse();
        policy.Data.DryRunDefault.Should().BeTrue();
    }

    [Fact]
    public async Task DryRunDoesNotDeleteAndDeleteRequiresExplicitConfirmation()
    {
        var stores = new Stores();
        stores.AuditLogs.Items.Add(Log("old", Now.AddDays(-181)));
        stores.AuditLogs.Items.Add(Log("recent", Now.AddDays(-1)));
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient admin = factory.CreateClient(); await AuthenticateAsync(admin, "retention-admin2@example.com", UserRole.Admin, stores);

        RunEnvelope dryRun = (await (await admin.PostAsJsonAsync("/api/v1/admin/audit-logs/retention/run", new RunAuditLogRetentionRequest(null, null, null))).Content.ReadFromJsonAsync<RunEnvelope>())!;
        HttpResponseMessage rejected = await admin.PostAsJsonAsync("/api/v1/admin/audit-logs/retention/run", new RunAuditLogRetentionRequest(180, false, false));

        dryRun.Data.Mode.Should().Be("DryRun");
        dryRun.Data.CandidateCount.Should().Be(1);
        dryRun.Data.DeletedCount.Should().Be(0);
        stores.AuditLogs.Items.Should().Contain(log => log.Id == "old").And.Contain(log => log.Id == "recent");
        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await rejected.Content.ReadAsStringAsync()).Should().Contain("validation_error");
    }

    [Fact]
    public async Task ConfirmedDeleteDeletesOnlyOldAuditLogsAndKeepsRuns()
    {
        var stores = new Stores();
        stores.AuditLogs.Items.Add(Log("old", Now.AddDays(-3651)));
        stores.AuditLogs.Items.Add(Log("recent", Now.AddDays(-1)));
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient admin = factory.CreateClient(); await AuthenticateAsync(admin, "retention-admin3@example.com", UserRole.Admin, stores);

        RunEnvelope deleted = (await (await admin.PostAsJsonAsync("/api/v1/admin/audit-logs/retention/run", new RunAuditLogRetentionRequest(3650, false, true))).Content.ReadFromJsonAsync<RunEnvelope>())!;
        RunsEnvelope runs = (await (await admin.GetAsync("/api/v1/admin/audit-logs/retention/runs")).Content.ReadFromJsonAsync<RunsEnvelope>())!;
        RunEnvelope fetched = (await (await admin.GetAsync($"/api/v1/admin/audit-logs/retention/runs/{deleted.Data.Id}")).Content.ReadFromJsonAsync<RunEnvelope>())!;

        deleted.Data.DeletedCount.Should().Be(1);
        stores.AuditLogs.Items.Select(log => log.Id).Should().Contain("recent").And.NotContain("old");
        stores.Runs.Items.Should().ContainSingle(run => run.Id == deleted.Data.Id);
        runs.Data.Items.Should().ContainSingle(run => run.Id == deleted.Data.Id);
        fetched.Data.Id.Should().Be(deleted.Data.Id);
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);
    private static AuditLogEntry Log(string id, DateTimeOffset createdAtUtc) => new() { Id = id, ActorUserId = "actor", ActorRole = "Rider", Action = AuditAction.AuthLogin, Module = AuditModule.Auth, EntityType = "User", EntityId = "actor", Outcome = AuditOutcome.Success, CreatedAtUtc = createdAtUtc };
    private static async Task<User> AuthenticateAsync(HttpClient client, string email, UserRole role, Stores stores) { const string secret = "StrongPass1!"; await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, secret, secret, "Moto User", null, role == UserRole.Monitor ? "Monitor" : "Rider", true)); User user = stores.Users.Items.Single(u => u.Email == email); user.Role = role; LoginEnvelope login = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, secret))).Content.ReadFromJsonAsync<LoginEnvelope>())!; client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Data.AccessToken); return user; }
    private static WebApplicationFactory<Program> CreateFactory(Stores stores) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Issuer"] = "MotoSOS", ["Jwt:Audience"] = "MotoSOS.Clients", ["Jwt:Key"] = new string('A', 48), ["Jwt:AccessTokenMinutes"] = "15", ["Jwt:RefreshTokenDays"] = "7", ["Jwt:RefreshTokenRememberMeDays"] = "30", ["MongoDb:" + "Connection" + "String"] = string.Empty, ["MongoDb:DatabaseName"] = "MotoSOS_Test" })); builder.ConfigureTestServices(services => { services.AddSingleton<IUserRepository>(stores.Users); services.AddSingleton<IRefreshTokenRepository>(stores.RefreshTokens); services.AddSingleton<IAuditLogRepository>(stores.AuditLogs); services.AddSingleton<IAuditLogRetentionRunRepository>(stores.Runs); }); });
    private sealed record LoginEnvelope(bool Success, LoginResponse Data);
    private sealed record PolicyEnvelope(bool Success, AuditLogRetentionPolicyResponse Data);
    private sealed record RunEnvelope(bool Success, AuditLogRetentionRunResponse Data);
    private sealed record RunsEnvelope(bool Success, GetAuditLogRetentionRunsResponse Data);
    private sealed class Stores { public Users Users { get; } = new(); public RefreshTokens RefreshTokens { get; } = new(); public AuditLogs AuditLogs { get; } = new(); public Runs Runs { get; } = new(); }
    private sealed class Users : IUserRepository { public List<User> Items { get; } = []; public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Id == id)); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Email == email)); public Task AddAsync(User u, CancellationToken ct) { Items.Add(u); return Task.CompletedTask; } public Task UpdateAsync(User u, CancellationToken ct) => Task.CompletedTask; }
    private sealed class RefreshTokens : IRefreshTokenRepository { public List<RefreshToken> Items { get; } = []; public Task<RefreshToken?> GetByHashAsync(string h, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.TokenHash == h)); public Task AddAsync(RefreshToken t, CancellationToken ct) { Items.Add(t); return Task.CompletedTask; } public Task UpdateAsync(RefreshToken t, CancellationToken ct) => Task.CompletedTask; }
    private sealed class AuditLogs : IAuditLogRepository { public List<AuditLogEntry> Items { get; } = []; public Task AddAsync(AuditLogEntry entry, CancellationToken ct) { Items.Add(entry); return Task.CompletedTask; } public Task<AuditLogEntry?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id)); public Task<IReadOnlyList<AuditLogEntry>> ListAsync(AuditLogQuery q, CancellationToken ct) => Task.FromResult<IReadOnlyList<AuditLogEntry>>(Items); public Task<long> CountAsync(AuditLogQuery q, CancellationToken ct) => Task.FromResult((long)Items.Count); public Task<long> CountOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken ct) => Task.FromResult((long)Items.Count(i => i.CreatedAtUtc < cutoffUtc)); public Task<long> DeleteOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken ct) { int count = Items.RemoveAll(i => i.CreatedAtUtc < cutoffUtc); return Task.FromResult((long)count); } }
    private sealed class Runs : IAuditLogRetentionRunRepository { public List<AuditLogRetentionRun> Items { get; } = []; public Task AddAsync(AuditLogRetentionRun run, CancellationToken ct) { Items.Add(run); return Task.CompletedTask; } public Task<AuditLogRetentionRun?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(run => run.Id == id)); public Task<IReadOnlyList<AuditLogRetentionRun>> ListAsync(AuditLogRetentionRunQuery q, CancellationToken ct) => Task.FromResult<IReadOnlyList<AuditLogRetentionRun>>(Items.OrderByDescending(run => run.CreatedAtUtc).Skip((q.PageNumber - 1) * q.PageSize).Take(q.PageSize).ToArray()); public Task<long> CountAsync(CancellationToken ct) => Task.FromResult((long)Items.Count); }
}
