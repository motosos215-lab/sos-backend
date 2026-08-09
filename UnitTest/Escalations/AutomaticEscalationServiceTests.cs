using FluentAssertions;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Modules.AlertAcknowledgements.Application;
using MotoSOS.API.Modules.AlertAcknowledgements.Domain;
using MotoSOS.API.Modules.AlertDispatch.Application;
using MotoSOS.API.Modules.AlertDispatch.Domain;
using MotoSOS.API.Modules.AuditLogs.Application;
using MotoSOS.API.Modules.AuditLogs.Contracts;
using MotoSOS.API.Modules.AuditLogs.Domain;
using MotoSOS.API.Modules.Escalations.Application;
using MotoSOS.API.Modules.Escalations.Contracts;
using MotoSOS.API.Modules.Escalations.Domain;
using MotoSOS.API.Modules.Escalations.Worker;
using MotoSOS.API.Modules.Incidents.Application;
using MotoSOS.API.Modules.Incidents.Domain;
using MotoSOS.API.Modules.Notifications.Application;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace UnitTest.Escalations;

public sealed class AutomaticEscalationServiceTests
{
    [Theory]
    [InlineData(IncidentStatus.Closed)]
    [InlineData(IncidentStatus.FalsePositiveCancelled)]
    public async Task DoesNotEscalateWhenIncidentIsNotOpen(IncidentStatus status)
    {
        Ctx c = Ctx.Create(incidentStatus: status, attempts: [Attempt(NotificationDeliveryStatus.SimulatedSent, Now.AddMinutes(-10))]);

        RunAutomaticEscalationResponse response = await c.Service.RunAsync(new RunAutomaticEscalationRequest(20, 300), "Worker", CancellationToken.None);

        response.NotReady.Should().Be(1);
        c.Escalations.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotEscalateWhenExistingEscalationAcknowledgedOrAttemptsAreNotReady()
    {
        (await Ctx.Create(existingEscalation: true, attempts: [Attempt(NotificationDeliveryStatus.SimulatedSent, Now.AddMinutes(-10))]).Service.RunAsync(new RunAutomaticEscalationRequest(20, 300), "Worker", CancellationToken.None)).AlreadyEscalated.Should().Be(1);
        (await Ctx.Create(acks: [Ack(AlertAcknowledgementStatus.Acknowledged)], attempts: [Attempt(NotificationDeliveryStatus.SimulatedSent, Now.AddMinutes(-10))]).Service.RunAsync(new RunAutomaticEscalationRequest(20, 300), "Worker", CancellationToken.None)).AlreadyAcknowledged.Should().Be(1);
        (await Ctx.Create().Service.RunAsync(new RunAutomaticEscalationRequest(20, 300), "Worker", CancellationToken.None)).NotReady.Should().Be(1);
        (await Ctx.Create(attempts: [Attempt(NotificationDeliveryStatus.Prepared, null)]).Service.RunAsync(new RunAutomaticEscalationRequest(20, 300), "Worker", CancellationToken.None)).NotReady.Should().Be(1);
    }

    [Fact]
    public async Task DoesNotEscalateUntilOldestSimulatedSentAtPassesThreshold()
    {
        Ctx c = Ctx.Create(dispatchRequestedAtUtc: Now.AddHours(-1), attempts: [Attempt(NotificationDeliveryStatus.SimulatedSent, Now.AddSeconds(-30))]);

        RunAutomaticEscalationResponse response = await c.Service.RunAsync(new RunAutomaticEscalationRequest(20, 300), "Worker", CancellationToken.None);

        response.NotReady.Should().Be(1);
        c.Escalations.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task EscalatesOldSimulatedSentAndUsesExpectedFieldsAndIdempotency()
    {
        Ctx c = Ctx.Create(attempts: [Attempt(NotificationDeliveryStatus.SimulatedSent, Now.AddMinutes(-10))]);

        RunAutomaticEscalationResponse first = await c.Service.RunAsync(new RunAutomaticEscalationRequest(20, 300), "Worker", CancellationToken.None);
        RunAutomaticEscalationResponse second = await c.Service.RunAsync(new RunAutomaticEscalationRequest(20, 300), "Worker", CancellationToken.None);

        first.Escalated.Should().Be(1);
        second.AlreadyEscalated.Should().Be(1);
        c.Escalations.Items.Should().ContainSingle();
        EmergencyEscalation escalation = c.Escalations.Items.Single();
        escalation.Reason.Should().Be(EmergencyEscalationReason.NoAcknowledgement);
        escalation.Level.Should().Be(EmergencyEscalationLevel.Level1);
        escalation.Status.Should().Be(EmergencyEscalationStatus.Requested);
        escalation.IdempotencyKey.Should().Be(new EmergencyEscalationIdempotencyKeyFactory().Create("rider", "dispatch"));
        c.Incidents.Items.Single().Status.Should().Be(IncidentStatus.Open);
        c.Dispatches.Items.Single().Status.Should().Be(AlertDispatchStatus.PendingDispatch);
        c.Attempts.Items.Should().ContainSingle();
        c.Acks.Items.Should().BeEmpty();
        c.Audit.Actions.Should().Contain(AuditAction.EmergencyEscalationAutomaticallyRequested).And.Contain(AuditAction.AutomaticEscalationWorkerRun);
    }

    [Fact]
    public async Task ContinuesWhenCandidateFailsAndAuditFailureDoesNotBreakRun()
    {
        AlertDispatchRequest broken = Dispatch("throw", "incident");
        Ctx c = Ctx.Create(audit: new FailingAudit(), extraDispatches: [broken], attempts: [Attempt(NotificationDeliveryStatus.SimulatedSent, Now.AddMinutes(-10))]);

        RunAutomaticEscalationResponse response = await c.Service.RunAsync(new RunAutomaticEscalationRequest(20, 300), "Worker", CancellationToken.None);

        response.Escalated.Should().Be(1);
        response.Failed.Should().Be(1);
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);
    private static NotificationDeliveryAttempt Attempt(NotificationDeliveryStatus status, DateTimeOffset? simulatedSentAtUtc) => new() { Id = Guid.NewGuid().ToString("N"), UserId = "rider", AlertDispatchId = "dispatch", IncidentId = "incident", TripId = "trip", EmergencyContactId = "contact", Status = status, CreatedAtUtc = Now.AddMinutes(-20), PreparedAtUtc = Now.AddMinutes(-20), SimulatedSentAtUtc = simulatedSentAtUtc };
    private static AlertAcknowledgement Ack(AlertAcknowledgementStatus status) => new() { Id = Guid.NewGuid().ToString("N"), UserId = "rider", AlertDispatchId = "dispatch", IncidentId = "incident", TripId = "trip", Status = status, CreatedAtUtc = Now };
    private static AlertDispatchRequest Dispatch(string id, string incidentId, DateTimeOffset? requestedAtUtc = null) => new() { Id = id, UserId = "rider", IncidentId = incidentId, TripId = "trip", Status = AlertDispatchStatus.PendingDispatch, RequestedAtUtc = requestedAtUtc ?? Now.AddMinutes(-20), CreatedAtUtc = requestedAtUtc ?? Now.AddMinutes(-20) };

    private sealed class Ctx
    {
        public Dispatches Dispatches { get; private set; } = null!; public Incidents Incidents { get; private set; } = null!; public Attempts Attempts { get; private set; } = null!; public Acks Acks { get; private set; } = null!; public Escalations Escalations { get; private set; } = null!; public Audit Audit { get; private set; } = null!; public AutomaticEscalationService Service { get; private set; } = null!;
        public static Ctx Create(IncidentStatus incidentStatus = IncidentStatus.Open, DateTimeOffset? dispatchRequestedAtUtc = null, NotificationDeliveryAttempt[]? attempts = null, AlertAcknowledgement[]? acks = null, bool existingEscalation = false, IAuditLogService? audit = null, AlertDispatchRequest[]? extraDispatches = null)
        {
            var c = new Ctx(); var dispatches = new List<AlertDispatchRequest> { Dispatch("dispatch", "incident", dispatchRequestedAtUtc) }; if (extraDispatches is not null) dispatches.AddRange(extraDispatches); c.Dispatches = new Dispatches([.. dispatches]); c.Incidents = new Incidents(new Incident { Id = "incident", UserId = "rider", TripId = "trip", Status = incidentStatus, CreatedAtUtc = Now }); c.Attempts = new Attempts(attempts ?? []); c.Acks = new Acks(acks ?? []); c.Escalations = new Escalations(); if (existingEscalation) c.Escalations.Items.Add(new EmergencyEscalation { UserId = "rider", AlertDispatchId = "dispatch", IncidentId = "incident", IdempotencyKey = new EmergencyEscalationIdempotencyKeyFactory().Create("rider", "dispatch") }); c.Audit = audit as Audit ?? new Audit(); c.Service = new AutomaticEscalationService(new Users(), c.Dispatches, c.Incidents, c.Attempts, c.Acks, c.Escalations, new EmergencyEscalationIdempotencyKeyFactory(), new Clock(), audit ?? c.Audit, new InMemoryAutomaticEscalationWorkerStateStore()); return c;
        }
    }

    private sealed class Clock : IClock { public DateTimeOffset UtcNow => Now; }
    private sealed class Users : IUserRepository { public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult<User?>(new User { Id = id, Role = UserRole.Admin, IsActive = true }); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult<User?>(null); public Task AddAsync(User user, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(User user, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Dispatches(params AlertDispatchRequest[] items) : IAlertDispatchRepository { public List<AlertDispatchRequest> Items { get; } = items.ToList(); public Task<AlertDispatchRequest?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id)); public Task<AlertDispatchRequest?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) => Task.FromResult<AlertDispatchRequest?>(null); public Task<(AlertDispatchRequest AlertDispatch, bool IsDuplicate)> AddOrGetDuplicateAsync(AlertDispatchRequest alertDispatch, CancellationToken ct) => throw new NotImplementedException(); public Task<IReadOnlyList<AlertDispatchRequest>> ListCandidatesForAutomaticEscalationAsync(DateTimeOffset cutoffUtc, int maxItems, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertDispatchRequest>>(Items.Where(i => i.Status == AlertDispatchStatus.PendingDispatch && i.RequestedAtUtc <= cutoffUtc).Take(maxItems).ToArray()); public Task<IReadOnlyList<AlertDispatchRequest>> ListByUserIdAsync(string userId, AlertDispatchStatus? status, string? incidentId, int pageNumber, int pageSize, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertDispatchRequest>>([]); public Task<long> CountByUserIdAsync(string userId, AlertDispatchStatus? status, string? incidentId, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(AlertDispatchRequest alertDispatch, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Incidents(params Incident[] items) : IIncidentRepository { public List<Incident> Items { get; } = items.ToList(); public Task<Incident?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id)); public Task<Incident?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) => Task.FromResult<Incident?>(null); public Task<(Incident Incident, bool IsDuplicate)> AddOrGetDuplicateAsync(Incident incident, CancellationToken ct) => throw new NotImplementedException(); public Task<IReadOnlyList<Incident>> ListByUserIdAsync(string userId, IncidentStatus? status, string? tripId, int pageNumber, int pageSize, CancellationToken ct) => Task.FromResult<IReadOnlyList<Incident>>([]); public Task<long> CountByUserIdAsync(string userId, IncidentStatus? status, string? tripId, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(Incident incident, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Attempts(params NotificationDeliveryAttempt[] items) : INotificationDeliveryAttemptRepository { public List<NotificationDeliveryAttempt> Items { get; } = items.ToList(); public Task<NotificationDeliveryAttempt?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id)); public Task<NotificationDeliveryAttempt?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) => Task.FromResult<NotificationDeliveryAttempt?>(null); public Task<(NotificationDeliveryAttempt Attempt, bool IsDuplicate)> AddOrGetDuplicateAsync(NotificationDeliveryAttempt attempt, CancellationToken ct) => throw new NotImplementedException(); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByAlertDispatchIdAsync(string userId, string alertDispatchId, CancellationToken ct) { if (alertDispatchId == "throw") throw new InvalidOperationException("candidate failed"); return Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>(Items.Where(i => i.UserId == userId && i.AlertDispatchId == alertDispatchId).ToArray()); } public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByUserIdAsync(string userId, string? alertDispatchId, string? incidentId, NotificationDeliveryStatus? status, int pageNumber, int pageSize, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>([]); public Task<long> CountByUserIdAsync(string userId, string? alertDispatchId, string? incidentId, NotificationDeliveryStatus? status, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(NotificationDeliveryAttempt attempt, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Acks(params AlertAcknowledgement[] items) : IAlertAcknowledgementRepository { public List<AlertAcknowledgement> Items { get; } = items.ToList(); public Task<AlertAcknowledgement?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id)); public Task<AlertAcknowledgement?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) => Task.FromResult<AlertAcknowledgement?>(null); public Task<(AlertAcknowledgement Acknowledgement, bool IsDuplicate)> AddOrGetDuplicateAsync(AlertAcknowledgement acknowledgement, CancellationToken ct) => throw new NotImplementedException(); public Task<IReadOnlyList<AlertAcknowledgement>> ListByAlertDispatchIdAsync(string userId, string alertDispatchId, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertAcknowledgement>>(Items.Where(i => i.UserId == userId && i.AlertDispatchId == alertDispatchId).ToArray()); public Task<IReadOnlyList<AlertAcknowledgement>> ListByMonitorUserIdAsync(string monitorUserId, AlertAcknowledgementStatus? status, int pageNumber, int pageSize, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertAcknowledgement>>([]); public Task<long> CountByMonitorUserIdAsync(string monitorUserId, AlertAcknowledgementStatus? status, CancellationToken ct) => Task.FromResult(0L); public Task<IReadOnlyList<AlertAcknowledgement>> ListByUserIdAsync(string userId, string? alertDispatchId, string? incidentId, AlertAcknowledgementStatus? status, int pageNumber, int pageSize, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertAcknowledgement>>([]); public Task<long> CountByUserIdAsync(string userId, string? alertDispatchId, string? incidentId, AlertAcknowledgementStatus? status, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(AlertAcknowledgement acknowledgement, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Escalations : IEmergencyEscalationRepository { public List<EmergencyEscalation> Items { get; } = []; public Task<EmergencyEscalation?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id)); public Task<EmergencyEscalation?> GetByAlertDispatchIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.AlertDispatchId == id)); public Task<EmergencyEscalation?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.IdempotencyKey == key)); public Task<(EmergencyEscalation Escalation, bool IsDuplicate)> AddOrGetDuplicateAsync(EmergencyEscalation e, CancellationToken ct) { EmergencyEscalation? existing = Items.FirstOrDefault(i => i.AlertDispatchId == e.AlertDispatchId || i.IdempotencyKey == e.IdempotencyKey); if (existing is not null) return Task.FromResult((existing, true)); Items.Add(e); return Task.FromResult((e, false)); } public Task<IReadOnlyList<EmergencyEscalation>> ListAsync(EmergencyEscalationQuery query, CancellationToken ct) => Task.FromResult<IReadOnlyList<EmergencyEscalation>>(Items); public Task<long> CountAsync(EmergencyEscalationQuery query, CancellationToken ct) => Task.FromResult((long)Items.Count); public Task UpdateAsync(EmergencyEscalation escalation, CancellationToken ct) => Task.CompletedTask; }
    private class Audit : IAuditLogService { public List<AuditAction> Actions { get; } = []; public virtual Task RecordAsync(string actorUserId, string actorRole, AuditAction action, AuditModule module, string entityType, string? entityId, AuditOutcome outcome, string? reason, string? requestPath, string? httpMethod, IReadOnlyDictionary<string, string>? metadata, CancellationToken cancellationToken) { Actions.Add(action); return Task.CompletedTask; } public Task<GetAuditLogsResponse> ListAsync(string adminUserId, AuditLogQuery query, CancellationToken cancellationToken) => throw new NotImplementedException(); public Task<AuditLogResponse> GetAsync(string adminUserId, string id, CancellationToken cancellationToken) => throw new NotImplementedException(); }
    private sealed class FailingAudit : Audit { public override Task RecordAsync(string actorUserId, string actorRole, AuditAction action, AuditModule module, string entityType, string? entityId, AuditOutcome outcome, string? reason, string? requestPath, string? httpMethod, IReadOnlyDictionary<string, string>? metadata, CancellationToken cancellationToken) => throw new InvalidOperationException("audit failed"); }
}
