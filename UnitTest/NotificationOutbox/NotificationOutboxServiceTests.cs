using FluentAssertions;
using Microsoft.Extensions.Options;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AuditLogs.Application;
using MotoSOS.API.Modules.AuditLogs.Contracts;
using MotoSOS.API.Modules.AuditLogs.Domain;
using MotoSOS.API.Modules.NotificationOutbox.Application;
using MotoSOS.API.Modules.NotificationOutbox.Contracts;
using MotoSOS.API.Modules.NotificationOutbox.Worker;
using MotoSOS.API.Modules.Notifications.Application;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.Notifications.Providers;
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
        prepared.ProviderMessageId.Should().StartWith("simulated-");
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
        prepared.Provider.Should().Be(NotificationProvider.Simulated);
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
        failed.ProviderMessageId.Should().BeNull();
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
        audit.Actions.Should().Contain(AuditAction.NotificationProviderSimulatedSent);
        audit.Metadata.Should().Contain(metadata => metadata != null && metadata.ContainsKey("processed") && metadata.ContainsKey("simulateFailures"));
        audit.Metadata.Should().Contain(metadata => metadata != null && metadata.ContainsKey("retried"));
    }

    [Fact]
    public async Task EmptyPreparedReturnsZeroCounts()
    {
        RunNotificationOutboxResponse response = await Service(User(UserRole.Admin), new Attempts()).RunAsync("admin", new RunNotificationOutboxRequest(null, false), CancellationToken.None);
        response.Processed.Should().Be(0);
        response.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ProviderFailureOrExceptionMarksFailedWithControlledReason()
    {
        var providerFailure = Attempt(NotificationDeliveryStatus.Prepared);
        await Service(User(UserRole.Admin), new Attempts(providerFailure)).RunAsync("admin", new RunNotificationOutboxRequest(20, true), CancellationToken.None);
        providerFailure.Status.Should().Be(NotificationDeliveryStatus.Failed);
        providerFailure.FailureReason.Should().Be("simulated_failure_requested");

        var providerException = Attempt(NotificationDeliveryStatus.Prepared);
        await Service(User(UserRole.Admin), new Attempts(providerException), providers: new ThrowingResolver()).RunAsync("admin", new RunNotificationOutboxRequest(20, false), CancellationToken.None);
        providerException.Status.Should().Be(NotificationDeliveryStatus.Failed);
        providerException.FailureReason.Should().Be("simulated_provider_failure");
    }

    [Fact]
    public async Task FcmProviderResultMarksAttemptAsLegacySentWithFcmProvider()
    {
        var prepared = Attempt(NotificationDeliveryStatus.Prepared); prepared.Channel = NotificationChannel.Push; var attempts = new Attempts(prepared);

        RunNotificationOutboxResponse response = await Service(User(UserRole.Admin), attempts, providers: new StaticResolver(new StaticProvider(new NotificationProviderResult(NotificationProviderType.Fcm, NotificationProviderChannel.Push, NotificationProviderDeliveryStatus.Sent, "fcm-message", "fcm-sent", null, null, Now, null)))).RunAsync("admin", new RunNotificationOutboxRequest(20, false), CancellationToken.None);

        response.SimulatedSent.Should().Be(1);
        prepared.Status.Should().Be(NotificationDeliveryStatus.SimulatedSent);
        prepared.Provider.Should().Be(NotificationProvider.Fcm);
        prepared.ProviderMessageId.Should().Be("fcm-message");
    }

    [Fact]
    public async Task FcmProviderFailureMarksAttemptFailedWithFcmProvider()
    {
        var prepared = Attempt(NotificationDeliveryStatus.Prepared); prepared.Channel = NotificationChannel.Push; var attempts = new Attempts(prepared);

        await Service(User(UserRole.Admin), attempts, providers: new StaticResolver(new StaticProvider(new NotificationProviderResult(NotificationProviderType.Fcm, NotificationProviderChannel.Push, NotificationProviderDeliveryStatus.Failed, null, "fcm-failed", "push_token_not_available", "safe", null, Now)))).RunAsync("admin", new RunNotificationOutboxRequest(20, false), CancellationToken.None);

        prepared.Status.Should().Be(NotificationDeliveryStatus.Failed);
        prepared.Provider.Should().Be(NotificationProvider.Fcm);
        prepared.FailureReason.Should().Be("push_token_not_available");
    }

    [Fact]
    public async Task AuditFailureDoesNotBreakRunOrRetry()
    {
        var failed = Attempt(NotificationDeliveryStatus.Failed);
        var prepared = Attempt(NotificationDeliveryStatus.Prepared);
        NotificationOutboxService service = Service(User(UserRole.Admin), new Attempts(failed, prepared), new FailingAudit());
        (await service.RunAsync("admin", new RunNotificationOutboxRequest(20, false), CancellationToken.None)).SimulatedSent.Should().Be(1);
        (await service.RetryFailedAsync("admin", new RetryFailedNotificationOutboxRequest(20), CancellationToken.None)).Retried.Should().Be(1);
    }

    [Fact]
    public async Task WorkerRunUsesSharedOutboxRulesWithoutAdminUser()
    {
        var prepared = Attempt(NotificationDeliveryStatus.Prepared);
        var failed = Attempt(NotificationDeliveryStatus.Failed);
        var audit = new Audit();
        NotificationOutboxService service = Service(User(UserRole.Rider), new Attempts(prepared, failed), audit);

        RunNotificationOutboxResponse response = await service.RunWorkerAsync(new RunNotificationOutboxRequest(20, false), 60, CancellationToken.None);

        response.SimulatedSent.Should().Be(1);
        prepared.Status.Should().Be(NotificationDeliveryStatus.SimulatedSent);
        failed.Status.Should().Be(NotificationDeliveryStatus.Failed);
        audit.Actions.Should().Contain(AuditAction.NotificationOutboxWorkerRun);
        audit.Metadata.Should().Contain(metadata => metadata != null && metadata.ContainsKey("runSource") && metadata["runSource"] == "Worker" && metadata.ContainsKey("intervalSeconds"));
    }

    [Fact]
    public async Task WorkerStatusRequiresAdminAndReturnsSafeState()
    {
        var state = new InMemoryNotificationOutboxWorkerStateStore();
        state.MarkStarted(Now.AddMinutes(-1));
        state.MarkSucceeded(Now, 3, 2, 1, 0);
        var options = Options.Create(new NotificationOutboxWorkerOptions { Enabled = true, IntervalSeconds = 30, MaxItemsPerRun = 20, SimulateFailures = false, RunOnStartup = true });
        User admin = User(UserRole.Admin);
        NotificationOutboxService service = new(new Users(admin), new Attempts(), new NotificationProviderResolver(new SimulatedNotificationProvider(new Clock())), new Clock(), workerState: state, workerOptions: options);

        NotificationOutboxWorkerStatusResponse response = await service.GetWorkerStatusAsync(admin.Id, CancellationToken.None);

        response.Enabled.Should().BeTrue();
        response.Running.Should().BeFalse();
        response.IntervalSeconds.Should().Be(30);
        response.MaxItemsPerRun.Should().Be(20);
        response.SimulateFailures.Should().BeFalse();
        response.RunOnStartup.Should().BeTrue();
        response.LastProcessedCount.Should().Be(3);
        response.LastFailedCount.Should().Be(1);
        response.LastError.Should().BeNull();
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);
    private static NotificationOutboxService Service(User user, Attempts attempts, IAuditLogService? audit = null, INotificationProviderResolver? providers = null) => new(new Users(user), attempts, providers ?? new NotificationProviderResolver(new SimulatedNotificationProvider(new Clock())), new Clock(), audit);
    private static User User(UserRole role) => new() { Id = role.ToString().ToLowerInvariant(), Role = role, IsActive = true, Email = $"{Guid.NewGuid()}@example.com" };
    private static NotificationDeliveryAttempt Attempt(NotificationDeliveryStatus status) => new() { Id = Guid.NewGuid().ToString("N"), Status = status, Channel = NotificationChannel.Sms, Provider = NotificationProvider.None, PreparedAtUtc = Now.AddMinutes(-10), CreatedAtUtc = Now.AddMinutes(-10), LastStatusChangedAtUtc = Now.AddMinutes(-10) };
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => Now; }
    private sealed class Users(User user) : IUserRepository { public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult<User?>(user.Id == id ? user : null); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult<User?>(null); public Task AddAsync(User user, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(User user, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Attempts(params NotificationDeliveryAttempt[] items) : INotificationDeliveryAttemptRepository
    {
        private readonly List<NotificationDeliveryAttempt> _items = items.ToList();
        public Task<NotificationDeliveryAttempt?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(_items.FirstOrDefault(a => a.Id == id)); public Task<NotificationDeliveryAttempt?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) => Task.FromResult<NotificationDeliveryAttempt?>(null); public Task<(NotificationDeliveryAttempt Attempt, bool IsDuplicate)> AddOrGetDuplicateAsync(NotificationDeliveryAttempt attempt, CancellationToken ct) => Task.FromResult((attempt, false)); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByUserIdAsync(string userId, string? alertDispatchId, string? incidentId, NotificationDeliveryStatus? status, int pageNumber, int pageSize, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>([]); public Task<long> CountByUserIdAsync(string userId, string? alertDispatchId, string? incidentId, NotificationDeliveryStatus? status, CancellationToken ct) => Task.FromResult(0L); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByStatusAsync(NotificationDeliveryStatus status, int maxItems, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>(_items.Where(a => a.Status == status).OrderBy(a => a.CreatedAtUtc).ThenBy(a => a.PreparedAtUtc).Take(maxItems).ToArray()); public Task<NotificationDeliveryAttempt?> TryMarkSimulatedSentAsync(string attemptId, DateTimeOffset now, CancellationToken ct) { NotificationDeliveryAttempt? a = _items.FirstOrDefault(x => x.Id == attemptId && x.Status == NotificationDeliveryStatus.Prepared); if (a is null) return Task.FromResult<NotificationDeliveryAttempt?>(null); a.Status = NotificationDeliveryStatus.SimulatedSent; a.Provider = NotificationProvider.Simulated; a.SimulatedSentAtUtc = now; a.LastStatusChangedAtUtc = now; a.UpdatedAtUtc = now; return Task.FromResult<NotificationDeliveryAttempt?>(a); }
        public Task<NotificationDeliveryAttempt?> TryMarkSimulatedSentAsync(string attemptId, string? providerMessageId, DateTimeOffset now, CancellationToken ct) { NotificationDeliveryAttempt? a = _items.FirstOrDefault(x => x.Id == attemptId && x.Status == NotificationDeliveryStatus.Prepared); if (a is null) return Task.FromResult<NotificationDeliveryAttempt?>(null); a.Status = NotificationDeliveryStatus.SimulatedSent; a.Provider = NotificationProvider.Simulated; a.ProviderMessageId = providerMessageId; a.SimulatedSentAtUtc = now; a.LastStatusChangedAtUtc = now; a.UpdatedAtUtc = now; return Task.FromResult<NotificationDeliveryAttempt?>(a); }
        public Task<NotificationDeliveryAttempt?> TryMarkSentAsync(string attemptId, NotificationProvider provider, string? providerMessageId, DateTimeOffset sentAtUtc, CancellationToken ct) { NotificationDeliveryAttempt? a = _items.FirstOrDefault(x => x.Id == attemptId && x.Status == NotificationDeliveryStatus.Prepared); if (a is null) return Task.FromResult<NotificationDeliveryAttempt?>(null); a.Status = NotificationDeliveryStatus.SimulatedSent; a.Provider = provider; a.ProviderMessageId = providerMessageId; a.SimulatedSentAtUtc = sentAtUtc; a.LastStatusChangedAtUtc = sentAtUtc; a.UpdatedAtUtc = sentAtUtc; return Task.FromResult<NotificationDeliveryAttempt?>(a); }
        public Task<NotificationDeliveryAttempt?> TryMarkFailedAsync(string attemptId, string reason, DateTimeOffset now, CancellationToken ct) { NotificationDeliveryAttempt? a = _items.FirstOrDefault(x => x.Id == attemptId && x.Status == NotificationDeliveryStatus.Prepared); if (a is null) return Task.FromResult<NotificationDeliveryAttempt?>(null); a.Status = NotificationDeliveryStatus.Failed; a.Provider = NotificationProvider.Simulated; a.ProviderMessageId = null; a.FailedAtUtc = now; a.FailureReason = reason; a.LastStatusChangedAtUtc = now; a.UpdatedAtUtc = now; return Task.FromResult<NotificationDeliveryAttempt?>(a); }
        public Task<NotificationDeliveryAttempt?> TryMarkFailedAsync(string attemptId, NotificationProvider provider, string reason, DateTimeOffset now, CancellationToken ct) { NotificationDeliveryAttempt? a = _items.FirstOrDefault(x => x.Id == attemptId && x.Status == NotificationDeliveryStatus.Prepared); if (a is null) return Task.FromResult<NotificationDeliveryAttempt?>(null); a.Status = NotificationDeliveryStatus.Failed; a.Provider = provider; a.ProviderMessageId = null; a.FailedAtUtc = now; a.FailureReason = reason; a.LastStatusChangedAtUtc = now; a.UpdatedAtUtc = now; return Task.FromResult<NotificationDeliveryAttempt?>(a); }
        public Task<NotificationDeliveryAttempt?> TryResetFailedToPreparedAsync(string attemptId, DateTimeOffset now, CancellationToken ct) { NotificationDeliveryAttempt? a = _items.FirstOrDefault(x => x.Id == attemptId && x.Status == NotificationDeliveryStatus.Failed); if (a is null) return Task.FromResult<NotificationDeliveryAttempt?>(null); a.Status = NotificationDeliveryStatus.Prepared; a.Provider = NotificationProvider.None; a.ProviderMessageId = null; a.FailedAtUtc = null; a.FailureReason = null; a.LastStatusChangedAtUtc = now; a.UpdatedAtUtc = now; return Task.FromResult<NotificationDeliveryAttempt?>(a); }
        public Task<long> CountByStatusAsync(NotificationDeliveryStatus status, CancellationToken ct) => Task.FromResult((long)_items.Count(a => a.Status == status)); public Task UpdateAsync(NotificationDeliveryAttempt attempt, CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class Audit : IAuditLogService { public List<AuditAction> Actions { get; } = []; public List<IReadOnlyDictionary<string, string>?> Metadata { get; } = []; public Task RecordAsync(string actorUserId, string actorRole, AuditAction action, AuditModule module, string entityType, string? entityId, AuditOutcome outcome, string? reason, string? requestPath, string? httpMethod, IReadOnlyDictionary<string, string>? metadata, CancellationToken cancellationToken) { Actions.Add(action); Metadata.Add(metadata); return Task.CompletedTask; } public Task<GetAuditLogsResponse> ListAsync(string adminUserId, AuditLogQuery query, CancellationToken cancellationToken) => throw new NotImplementedException(); public Task<AuditLogResponse> GetAsync(string adminUserId, string id, CancellationToken cancellationToken) => throw new NotImplementedException(); }
    private sealed class FailingAudit : IAuditLogService { public Task RecordAsync(string actorUserId, string actorRole, AuditAction action, AuditModule module, string entityType, string? entityId, AuditOutcome outcome, string? reason, string? requestPath, string? httpMethod, IReadOnlyDictionary<string, string>? metadata, CancellationToken cancellationToken) => throw new InvalidOperationException("audit failed"); public Task<GetAuditLogsResponse> ListAsync(string adminUserId, AuditLogQuery query, CancellationToken cancellationToken) => throw new NotImplementedException(); public Task<AuditLogResponse> GetAsync(string adminUserId, string id, CancellationToken cancellationToken) => throw new NotImplementedException(); }
    private sealed class ThrowingResolver : INotificationProviderResolver { public INotificationProvider Resolve(NotificationProviderChannel channel) => new ThrowingProvider(); }
    private sealed class ThrowingProvider : INotificationProvider { public NotificationProviderType ProviderType => NotificationProviderType.Simulated; public Task<NotificationProviderResult> SendAsync(NotificationProviderRequest request, CancellationToken cancellationToken) => throw new InvalidOperationException("provider failed"); }
    private sealed class StaticResolver(INotificationProvider provider) : INotificationProviderResolver { public INotificationProvider Resolve(NotificationProviderChannel channel) => provider; }
    private sealed class StaticProvider(NotificationProviderResult result) : INotificationProvider { public NotificationProviderType ProviderType => result.ProviderType; public Task<NotificationProviderResult> SendAsync(NotificationProviderRequest request, CancellationToken cancellationToken) => Task.FromResult(result); }
}
