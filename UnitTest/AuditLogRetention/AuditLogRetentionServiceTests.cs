using FluentAssertions;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AuditLogRetention.Application;
using MotoSOS.API.Modules.AuditLogRetention.Contracts;
using MotoSOS.API.Modules.AuditLogRetention.Domain;
using MotoSOS.API.Modules.AuditLogs.Application;
using MotoSOS.API.Modules.AuditLogs.Contracts;
using MotoSOS.API.Modules.AuditLogs.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace UnitTest.AuditLogRetention;

public sealed class AuditLogRetentionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PolicyRequiresAdminAndReturnsCodeDefaults()
    {
        Ctx c = Ctx.Create();

        AuditLogRetentionPolicyResponse policy = await c.Service.GetPolicyAsync("admin", CancellationToken.None);

        policy.RetentionDaysDefault.Should().Be(180);
        policy.MinimumRetentionDays.Should().Be(90);
        policy.MaximumRetentionDays.Should().Be(3650);
        policy.DryRunDefault.Should().BeTrue();
        policy.DeleteRequiresConfirmation.Should().BeTrue();
        policy.AutomaticWorkerEnabled.Should().BeFalse();
        await Assert.ThrowsAsync<ForbiddenAppException>(() => c.Service.GetPolicyAsync("rider", CancellationToken.None));
    }

    [Fact]
    public async Task DryRunCountsOldLogsButDoesNotDelete()
    {
        Ctx c = Ctx.Create(Log("old", Now.AddDays(-181)), Log("recent", Now.AddDays(-10)));

        AuditLogRetentionRunResponse response = await c.Service.RunAsync("admin", new ValidatedAuditLogRetentionRunRequest(180, true, false), CancellationToken.None);

        response.Mode.Should().Be("DryRun");
        response.CandidateCount.Should().Be(1);
        response.DeletedCount.Should().Be(0);
        c.AuditLogs.Items.Select(log => log.Id).Should().Contain(["old", "recent"]);
        c.Runs.Items.Should().ContainSingle(run => run.Id == response.Id);
        c.Audit.Metadata.Should().ContainKey("retentionRunId").And.ContainKey("candidateCount").And.ContainKey("deletedCount");
    }

    [Fact]
    public async Task DeleteWithConfirmationDeletesOnlyLogsOlderThanCutoff()
    {
        Ctx c = Ctx.Create(Log("old", Now.AddDays(-181)), Log("equal", Now.AddDays(-180)), Log("recent", Now.AddDays(-1)));

        AuditLogRetentionRunResponse response = await c.Service.RunAsync("admin", new ValidatedAuditLogRetentionRunRequest(180, false, true), CancellationToken.None);

        response.Mode.Should().Be("Delete");
        response.CandidateCount.Should().Be(1);
        response.DeletedCount.Should().Be(1);
        c.AuditLogs.Items.Select(log => log.Id).Should().Equal("equal", "recent");
        c.Runs.Items.Should().ContainSingle(run => run.Id == response.Id);
    }

    [Fact]
    public async Task RunsCanBeListedAndFetchedWithoutAuditLogMetadata()
    {
        Ctx c = Ctx.Create(Log("old", Now.AddDays(-181)));
        AuditLogRetentionRunResponse created = await c.Service.RunAsync("admin", new ValidatedAuditLogRetentionRunRequest(180, true, false), CancellationToken.None);

        GetAuditLogRetentionRunsResponse list = await c.Service.ListRunsAsync("admin", new AuditLogRetentionRunQuery(1, 10), CancellationToken.None);
        AuditLogRetentionRunResponse fetched = await c.Service.GetRunAsync("admin", created.Id, CancellationToken.None);

        list.Items.Should().ContainSingle(run => run.Id == created.Id);
        fetched.Id.Should().Be(created.Id);
        typeof(AuditLogRetentionRunResponse).GetProperties().Select(property => property.Name).Should().NotContain("Metadata");
    }

    [Fact]
    public async Task AuditWriteFailureDoesNotBreakRetentionRun()
    {
        Ctx c = Ctx.Create(failingAudit: true, logs: [Log("old", Now.AddDays(-181))]);

        AuditLogRetentionRunResponse response = await c.Service.RunAsync("admin", new ValidatedAuditLogRetentionRunRequest(180, true, false), CancellationToken.None);

        response.Status.Should().Be("Completed");
        c.Runs.Items.Should().ContainSingle();
    }

    private static AuditLogEntry Log(string id, DateTimeOffset createdAtUtc) => new() { Id = id, ActorUserId = "actor", ActorRole = "Rider", Action = AuditAction.AuthLogin, Module = AuditModule.Auth, EntityType = "User", EntityId = "actor", Outcome = AuditOutcome.Success, CreatedAtUtc = createdAtUtc };
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => Now; }
    private sealed class Users : IUserRepository { public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult<User?>(id switch { "admin" => new User { Id = "admin", Role = UserRole.Admin, IsActive = true }, "rider" => new User { Id = "rider", Role = UserRole.Rider, IsActive = true }, _ => null }); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult<User?>(null); public Task AddAsync(User user, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(User user, CancellationToken ct) => Task.CompletedTask; }
    private sealed class AuditLogs(params AuditLogEntry[] logs) : IAuditLogRepository { public List<AuditLogEntry> Items { get; } = logs.ToList(); public Task AddAsync(AuditLogEntry entry, CancellationToken ct) { Items.Add(entry); return Task.CompletedTask; } public Task<AuditLogEntry?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(log => log.Id == id)); public Task<IReadOnlyList<AuditLogEntry>> ListAsync(AuditLogQuery query, CancellationToken ct) => Task.FromResult<IReadOnlyList<AuditLogEntry>>(Items); public Task<long> CountAsync(AuditLogQuery query, CancellationToken ct) => Task.FromResult((long)Items.Count); public Task<long> CountOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken ct) => Task.FromResult((long)Items.Count(log => log.CreatedAtUtc < cutoffUtc)); public Task<long> DeleteOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken ct) { int deleted = Items.RemoveAll(log => log.CreatedAtUtc < cutoffUtc); return Task.FromResult((long)deleted); } }
    private sealed class Runs : IAuditLogRetentionRunRepository { public List<AuditLogRetentionRun> Items { get; } = []; public Task AddAsync(AuditLogRetentionRun run, CancellationToken ct) { Items.Add(run); return Task.CompletedTask; } public Task<AuditLogRetentionRun?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(run => run.Id == id)); public Task<IReadOnlyList<AuditLogRetentionRun>> ListAsync(AuditLogRetentionRunQuery query, CancellationToken ct) => Task.FromResult<IReadOnlyList<AuditLogRetentionRun>>(Items.OrderByDescending(run => run.CreatedAtUtc).ToArray()); public Task<long> CountAsync(CancellationToken ct) => Task.FromResult((long)Items.Count); }
    private class Audit : IAuditLogService { public IReadOnlyDictionary<string, string> Metadata { get; private set; } = new Dictionary<string, string>(); public virtual Task RecordAsync(string actorUserId, string actorRole, AuditAction action, AuditModule module, string entityType, string? entityId, AuditOutcome outcome, string? reason, string? requestPath, string? httpMethod, IReadOnlyDictionary<string, string>? metadata, CancellationToken cancellationToken) { Metadata = metadata ?? new Dictionary<string, string>(); return Task.CompletedTask; } public Task<GetAuditLogsResponse> ListAsync(string adminUserId, AuditLogQuery query, CancellationToken cancellationToken) => throw new NotImplementedException(); public Task<AuditLogResponse> GetAsync(string adminUserId, string id, CancellationToken cancellationToken) => throw new NotImplementedException(); }
    private sealed class FailingAudit : Audit { public override Task RecordAsync(string actorUserId, string actorRole, AuditAction action, AuditModule module, string entityType, string? entityId, AuditOutcome outcome, string? reason, string? requestPath, string? httpMethod, IReadOnlyDictionary<string, string>? metadata, CancellationToken cancellationToken) => throw new InvalidOperationException("audit failed"); }
    private sealed class Ctx { public AuditLogs AuditLogs { get; private init; } = null!; public Runs Runs { get; private init; } = null!; public Audit Audit { get; private init; } = null!; public AuditLogRetentionService Service { get; private init; } = null!; public static Ctx Create(params AuditLogEntry[] logs) => Create(false, logs); public static Ctx Create(bool failingAudit, params AuditLogEntry[] logs) { var auditLogs = new AuditLogs(logs); var runs = new Runs(); Audit audit = failingAudit ? new FailingAudit() : new Audit(); return new Ctx { AuditLogs = auditLogs, Runs = runs, Audit = audit, Service = new AuditLogRetentionService(new Users(), auditLogs, runs, new Clock(), audit) }; } }
}
