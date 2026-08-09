using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AuditLogs.Application;
using MotoSOS.API.Modules.AuditLogs.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace UnitTest.AuditLogs;

public sealed class AuditLogServiceTests
{
    [Fact]
    public async Task AdminCanListAuditLogsAndRiderMonitorAreForbidden()
    {
        var repository = new AuditLogs(Entry(action: AuditAction.IncidentClosed));
        (await Service(User(UserRole.Admin), repository).ListAsync("admin", Query(), CancellationToken.None)).TotalCount.Should().Be(1);
        await Assert.ThrowsAsync<ForbiddenAppException>(() => Service(User(UserRole.Rider), repository).ListAsync("rider", Query(), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenAppException>(() => Service(User(UserRole.Monitor), repository).ListAsync("monitor", Query(), CancellationToken.None));
    }

    [Fact]
    public async Task GetByIdReturnsLogAndMissingThrowsNotFound()
    {
        AuditLogEntry entry = Entry(id: "log-1");
        AuditLogService service = Service(User(UserRole.Admin), new AuditLogs(entry));
        (await service.GetAsync("admin", "log-1", CancellationToken.None)).Id.Should().Be("log-1");
        await Assert.ThrowsAsync<NotFoundAppException>(() => service.GetAsync("admin", "missing", CancellationToken.None));
    }

    [Fact]
    public async Task FiltersAndPaginationWorkWithDescendingOrder()
    {
        AuditLogEntry old = Entry(id: "old", actorUserId: "actor-1", action: AuditAction.IncidentClosed, module: AuditModule.Incidents, outcome: AuditOutcome.Success, entityType: "Incident", entityId: "incident-1", createdAtUtc: Now.AddMinutes(-1));
        AuditLogEntry recent = Entry(id: "recent", actorUserId: "actor-1", action: AuditAction.IncidentClosed, module: AuditModule.Incidents, outcome: AuditOutcome.Success, entityType: "Incident", entityId: "incident-1", createdAtUtc: Now);
        var repo = new AuditLogs(old, recent, Entry(id: "other", actorUserId: "actor-2", action: AuditAction.AuthLogin));
        var query = new AuditLogQuery("actor-1", AuditAction.IncidentClosed, AuditModule.Incidents, AuditOutcome.Success, "Incident", "incident-1", Now.AddHours(-1), Now.AddHours(1), 1, 1);

        var response = await Service(User(UserRole.Admin), repo).ListAsync("admin", query, CancellationToken.None);

        response.AuditLogs.Single().Id.Should().Be("recent");
        response.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task RecordAsyncStoresSanitizedMetadata()
    {
        var repo = new AuditLogs();
        await Service(User(UserRole.Admin), repo).RecordAsync("admin", "Admin", AuditAction.NotificationOutboxRun, AuditModule.NotificationOutbox, "NotificationOutbox", null, AuditOutcome.Success, null, null, null, new Dictionary<string, string> { ["processed"] = "1", ["accessToken"] = "secret", ["password"] = "secret", ["deviceIdentifier"] = "secret", ["providerToken"] = "secret", ["safeLong"] = new string('a', 250) }, CancellationToken.None);

        AuditLogEntry saved = repo.Items.Single();
        saved.Metadata.Should().ContainKey("processed");
        saved.Metadata.Should().ContainKey("safeLong");
        saved.Metadata["safeLong"].Length.Should().Be(AuditLogService.MetadataValueMaxLength);
        saved.Metadata.Keys.Should().NotContain(k => k.Contains("token", StringComparison.OrdinalIgnoreCase) || k.Contains("password", StringComparison.OrdinalIgnoreCase) || k.Contains("deviceIdentifier", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RecordAsyncDoesNotThrowWhenRepositoryFails()
    {
        Func<Task> act = () => Service(User(UserRole.Admin), new FailingAuditLogs()).RecordAsync("admin", "Admin", AuditAction.NotificationOutboxRun, AuditModule.NotificationOutbox, "NotificationOutbox", null, AuditOutcome.Success, null, null, null, null, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);
    private static AuditLogQuery Query() => new(null, null, null, null, null, null, null, null, 1, 20);
    private static AuditLogService Service(User user, IAuditLogRepository logs) => new(new Users(user), logs, new Clock(), NullLogger<AuditLogService>.Instance);
    private static User User(UserRole role) => new() { Id = role.ToString().ToLowerInvariant(), Role = role, IsActive = true, Email = $"{role}@example.com" };
    private static AuditLogEntry Entry(string id = "log", string actorUserId = "actor", AuditAction action = AuditAction.AuthLogin, AuditModule module = AuditModule.Auth, AuditOutcome outcome = AuditOutcome.Success, string entityType = "User", string? entityId = "entity", DateTimeOffset? createdAtUtc = null) => new() { Id = id, ActorUserId = actorUserId, ActorRole = "Admin", Action = action, Module = module, Outcome = outcome, EntityType = entityType, EntityId = entityId, CreatedAtUtc = createdAtUtc ?? Now };
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => Now; }
    private sealed class Users(User user) : IUserRepository { public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult<User?>(user.Id == id ? user : null); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult<User?>(null); public Task AddAsync(User user, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(User user, CancellationToken ct) => Task.CompletedTask; }
    private sealed class AuditLogs(params AuditLogEntry[] entries) : IAuditLogRepository
    {
        public List<AuditLogEntry> Items { get; } = entries.ToList();
        public Task AddAsync(AuditLogEntry entry, CancellationToken ct) { Items.Add(entry); return Task.CompletedTask; }
        public Task<AuditLogEntry?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(e => e.Id == id));
        public Task<IReadOnlyList<AuditLogEntry>> ListAsync(AuditLogQuery q, CancellationToken ct) => Task.FromResult<IReadOnlyList<AuditLogEntry>>(Apply(q).OrderByDescending(e => e.CreatedAtUtc).Skip((q.PageNumber - 1) * q.PageSize).Take(q.PageSize).ToArray());
        public Task<long> CountAsync(AuditLogQuery q, CancellationToken ct) => Task.FromResult((long)Apply(q).Count());
        public Task<long> CountOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken ct) => Task.FromResult((long)Items.Count(e => e.CreatedAtUtc < cutoffUtc));
        public Task<long> DeleteOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken ct) { int count = Items.RemoveAll(e => e.CreatedAtUtc < cutoffUtc); return Task.FromResult((long)count); }
        private IEnumerable<AuditLogEntry> Apply(AuditLogQuery q) => Items.Where(e => (q.ActorUserId is null || e.ActorUserId == q.ActorUserId) && (!q.Action.HasValue || e.Action == q.Action) && (!q.Module.HasValue || e.Module == q.Module) && (!q.Outcome.HasValue || e.Outcome == q.Outcome) && (q.EntityType is null || e.EntityType == q.EntityType) && (q.EntityId is null || e.EntityId == q.EntityId) && (!q.DateFrom.HasValue || e.CreatedAtUtc >= q.DateFrom) && (!q.DateTo.HasValue || e.CreatedAtUtc <= q.DateTo));
    }
    private sealed class FailingAuditLogs : IAuditLogRepository { public Task AddAsync(AuditLogEntry entry, CancellationToken ct) => throw new InvalidOperationException("store failed"); public Task<AuditLogEntry?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult<AuditLogEntry?>(null); public Task<IReadOnlyList<AuditLogEntry>> ListAsync(AuditLogQuery query, CancellationToken ct) => Task.FromResult<IReadOnlyList<AuditLogEntry>>([]); public Task<long> CountAsync(AuditLogQuery query, CancellationToken ct) => Task.FromResult(0L); public Task<long> CountOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken ct) => Task.FromResult(0L); public Task<long> DeleteOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken ct) => Task.FromResult(0L); }
}
