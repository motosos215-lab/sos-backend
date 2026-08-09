using FluentAssertions;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AuditLogs.Application;
using MotoSOS.API.Modules.AuditLogs.Contracts;
using MotoSOS.API.Modules.AuditLogs.Domain;
using MotoSOS.API.Modules.NotificationOutbox.Application;
using MotoSOS.API.Modules.NotificationOutbox.Contracts;
using MotoSOS.API.Modules.Notifications.Application;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace UnitTest.NotificationOutbox;

public sealed class NotificationOutboxServiceTests
{
    [Fact]
    public async Task AdminCanRunAndRiderMonitorAreForbidden()
    {
        var attempts = new Attempts(Attempt(NotificationDeliveryStatus.Prepared));
        (await Service(User(UserRole.Admin), attempts).RunAsync("admin", new RunNotificationOutboxRequest(null, false), CancellationToken.None)).SimulatedSent.Should().Be(1);
        await Assert.ThrowsAsync<ForbiddenAppException>(() => Service(User(UserRole.Rider), attempts).RunAsync("rider", new RunNotificationOutboxRequest(null, false), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenAppException>(() => Service(User(UserRole.Monitor), attempts).RunAsync("monitor", new RunNotificationOutboxRequest(null, false), CancellationToken.None));
    }

    [Fact]
    public async Task RunProcessesPreparedOnlyAndIsIdempotent()
    {
        var prepared = Attempt(NotificationDeliveryStatus.Prepared); var cancelled = Attempt(NotificationDeliveryStatus.Cancelled); var failed = Attempt(NotificationDeliveryStatus.Failed); var sent = Attempt(NotificationDeliveryStatus.SimulatedSent); var attempts = new Attempts(prepared, cancelled, failed, sent);

        RunNotificationOutboxResponse first = await Service(User(UserRole.Admin), attempts).RunAsync("admin", new RunNotificationOutboxRequest(20, false), CancellationToken.None);
        RunNotificationOutboxResponse second = await Service(User(UserRole.Admin), attempts).RunAsync("admin", new RunNotificationOutboxRequest(20, false), CancellationToken.None);

        first.Processed.Should().Be(1);
        first.SimulatedSent.Should().Be(1);
        second.Processed.Should().Be(0);
        prepared.Status.Should().Be(NotificationDeliveryStatus.SimulatedSent);
        cancelled.Status.Should().Be(NotificationDeliveryStatus.Cancelled);
        failed.Status.Should().Be(NotificationDeliveryStatus.Failed);
        sent.Status.Should().Be(NotificationDeliveryStatus.SimulatedSent);
    }

    [Fact]
    public async Task SimulateFailuresMarksPreparedFailedWithControlledReason()
    {
        var prepared = Attempt(NotificationDeliveryStatus.Prepared); var attempts = new Attempts(prepared);

        RunNotificationOutboxResponse response = await Service(User(UserRole.Admin), attempts).RunAsync("admin", new RunNotificationOutboxRequest(20, true), CancellationToken.None);

        response.Failed.Should().Be(1);
        prepared.Status.Should().Be(NotificationDeliveryStatus.Failed);
        prepared.FailureReason.Should().Be("simulated_failure_requested");
        prepared.FailedAtUtc.Should().Be(Now);
    }

    [Fact]
    public async Task StatusCountsAllOutboxStates()
    {
        var attempts = new Attempts(Attempt(NotificationDeliveryStatus.Prepared), Attempt(NotificationDeliveryStatus.SimulatedSent), Attempt(NotificationDeliveryStatus.Failed), Attempt(NotificationDeliveryStatus.Cancelled));

        GetNotificationOutboxStatusResponse response = await Service(User(UserRole.Admin), attempts).GetStatusAsync("admin", CancellationToken.None);

        response.Prepared.Should().Be(1);
        response.SimulatedSent.Should().Be(1);
        response.Failed.Should().Be(1);
        response.Cancelled.Should().Be(1);
    }

    [Fact]
    public async Task RetryFailedReturnsFailedToPreparedOnly()
    {
        var failed = Attempt(NotificationDeliveryStatus.Failed); failed.FailureReason = "failure"; failed.FailedAtUtc = Now.AddMinutes(-1); var cancelled = Attempt(NotificationDeliveryStatus.Cancelled); var sent = Attempt(NotificationDeliveryStatus.SimulatedSent); var attempts = new Attempts(failed, cancelled, sent);

        RetryFailedNotificationOutboxResponse response = await Service(User(UserRole.Admin), attempts).RetryFailedAsync("admin", new RetryFailedNotificationOutboxRequest(20), CancellationToken.None);

        response.Retried.Should().Be(1);
        failed.Status.Should().Be(NotificationDeliveryStatus.Prepared);
        failed.FailureReason.Should().BeNull();
        failed.FailedAtUtc.Should().BeNull();
        cancelled.Status.Should().Be(NotificationDeliveryStatus.Cancelled);
        sent.Status.Should().Be(NotificationDeliveryStatus.SimulatedSent);
    }

    [Fact]
    public async Task RunAndRetryFailedWriteAuditLogs()
    {
        var failed = Attempt(NotificationDeliveryStatus.Failed);
        var prepared = Attempt(NotificationDeliveryStatus.Prepared);
        var attempts = new Attempts(failed, prepared);
        var audit = new Audit();
        NotificationOutboxService service = Service(User(UserRole.Admin), attempts, audit);

        await service.RunAsync("admin", new RunNotificationOutboxRequest(20, false), CancellationToken.None);
        await service.RetryFailedAsync("admin", new RetryFailedNotificationOutboxRequest(20), CancellationToken.None);

        audit.Actions.Should().Contain(AuditAction.NotificationOutboxRun).And.Contain(AuditAction.NotificationOutboxRetryFailed);
        audit.Metadata.Should().Contain(metadata => metadata.ContainsKey("processed") && metadata.ContainsKey("simulateFailures"));
        audit.Metadata.Should().Contain(metadata => metadata.ContainsKey("retried"));
    }

    [Fact]
    public async Task EmptyPreparedReturnsZeroCounts()
    {
        RunNotificationOutboxResponse response = await Service(User(UserRole.Admin), new Attempts()).RunAsync("admin", new RunNotificationOutboxRequest(null, false), CancellationToken.None);
        response.Processed.Should().Be(0);
        response.Items.Should().BeEmpty();
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);
    private static NotificationOutboxService Service(User user, Attempts attempts, IAuditLogService? audit = null) => new(new Users(user), attempts, new Clock(), audit);
    private static User User(UserRole role) => new() { Id = role.ToString().ToLowerInvariant(), Role = role, IsActive = true, Email = $"{Guid.NewGuid()}@example.com" };
    private static NotificationDeliveryAttempt Attempt(NotificationDeliveryStatus status) => new() { Id = Guid.NewGuid().ToString("N"), Status = status, Channel = NotificationChannel.Sms, Provider = NotificationProvider.None, PreparedAtUtc = Now.AddMinutes(-10), CreatedAtUtc = Now.AddMinutes(-10), LastStatusChangedAtUtc = Now.AddMinutes(-10) };
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => Now; }
    private sealed class Users(User user) : IUserRepository { public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult<User?>(user.Id == id ? user : null); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult<User?>(null); public Task AddAsync(User user, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(User user, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Attempts(params NotificationDeliveryAttempt[] items) : INotificationDeliveryAttemptRepository
    {
        private readonly List<NotificationDeliveryAttempt> _items = items.ToList();
        public Task<NotificationDeliveryAttempt?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(_items.FirstOrDefault(a => a.Id == id)); public Task<NotificationDeliveryAttempt?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) => Task.FromResult<NotificationDeliveryAttempt?>(null); public Task<(NotificationDeliveryAttempt Attempt, bool IsDuplicate)> AddOrGetDuplicateAsync(NotificationDeliveryAttempt attempt, CancellationToken ct) => Task.FromResult((attempt, false)); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByUserIdAsync(string userId, string? alertDispatchId, string? incidentId, NotificationDeliveryStatus? status, int pageNumber, int pageSize, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>([]); public Task<long> CountByUserIdAsync(string userId, string? alertDispatchId, string? incidentId, NotificationDeliveryStatus? status, CancellationToken ct) => Task.FromResult(0L); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByStatusAsync(NotificationDeliveryStatus status, int maxItems, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>(_items.Where(a => a.Status == status).OrderBy(a => a.CreatedAtUtc).ThenBy(a => a.PreparedAtUtc).Take(maxItems).ToArray()); public Task<NotificationDeliveryAttempt?> TryMarkSimulatedSentAsync(string attemptId, DateTimeOffset now, CancellationToken ct) { NotificationDeliveryAttempt? a = _items.FirstOrDefault(x => x.Id == attemptId && x.Status == NotificationDeliveryStatus.Prepared); if (a is null) return Task.FromResult<NotificationDeliveryAttempt?>(null); a.Status = NotificationDeliveryStatus.SimulatedSent; a.Provider = NotificationProvider.Simulated; a.SimulatedSentAtUtc = now; a.LastStatusChangedAtUtc = now; a.UpdatedAtUtc = now; return Task.FromResult<NotificationDeliveryAttempt?>(a); }
        public Task<NotificationDeliveryAttempt?> TryMarkFailedAsync(string attemptId, string reason, DateTimeOffset now, CancellationToken ct) { NotificationDeliveryAttempt? a = _items.FirstOrDefault(x => x.Id == attemptId && x.Status == NotificationDeliveryStatus.Prepared); if (a is null) return Task.FromResult<NotificationDeliveryAttempt?>(null); a.Status = NotificationDeliveryStatus.Failed; a.FailedAtUtc = now; a.FailureReason = reason; a.LastStatusChangedAtUtc = now; a.UpdatedAtUtc = now; return Task.FromResult<NotificationDeliveryAttempt?>(a); }
        public Task<NotificationDeliveryAttempt?> TryResetFailedToPreparedAsync(string attemptId, DateTimeOffset now, CancellationToken ct) { NotificationDeliveryAttempt? a = _items.FirstOrDefault(x => x.Id == attemptId && x.Status == NotificationDeliveryStatus.Failed); if (a is null) return Task.FromResult<NotificationDeliveryAttempt?>(null); a.Status = NotificationDeliveryStatus.Prepared; a.Provider = NotificationProvider.None; a.FailedAtUtc = null; a.FailureReason = null; a.LastStatusChangedAtUtc = now; a.UpdatedAtUtc = now; return Task.FromResult<NotificationDeliveryAttempt?>(a); }
        public Task<long> CountByStatusAsync(NotificationDeliveryStatus status, CancellationToken ct) => Task.FromResult((long)_items.Count(a => a.Status == status)); public Task UpdateAsync(NotificationDeliveryAttempt attempt, CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class Audit : IAuditLogService { public List<AuditAction> Actions { get; } = []; public List<IReadOnlyDictionary<string, string>?> Metadata { get; } = []; public Task RecordAsync(string actorUserId, string actorRole, AuditAction action, AuditModule module, string entityType, string? entityId, AuditOutcome outcome, string? reason, string? requestPath, string? httpMethod, IReadOnlyDictionary<string, string>? metadata, CancellationToken cancellationToken) { Actions.Add(action); Metadata.Add(metadata); return Task.CompletedTask; } public Task<GetAuditLogsResponse> ListAsync(string adminUserId, AuditLogQuery query, CancellationToken cancellationToken) => throw new NotImplementedException(); public Task<AuditLogResponse> GetAsync(string adminUserId, string id, CancellationToken cancellationToken) => throw new NotImplementedException(); }
}
