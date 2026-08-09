using FluentAssertions;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AlertAcknowledgements.Application;
using MotoSOS.API.Modules.AlertAcknowledgements.Domain;
using MotoSOS.API.Modules.AlertDispatch.Application;
using MotoSOS.API.Modules.AlertDispatch.Domain;
using MotoSOS.API.Modules.AuditLogs.Application;
using MotoSOS.API.Modules.AuditLogs.Contracts;
using MotoSOS.API.Modules.AuditLogs.Domain;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
using MotoSOS.API.Modules.Escalations.Application;
using MotoSOS.API.Modules.Escalations.Contracts;
using MotoSOS.API.Modules.Escalations.Domain;
using MotoSOS.API.Modules.Incidents.Application;
using MotoSOS.API.Modules.Incidents.Domain;
using MotoSOS.API.Modules.Notifications.Application;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace UnitTest.Escalations;

public sealed class EmergencyEscalationServiceTests
{
    [Fact]
    public async Task RiderCanCreateManualEscalationForOwnOpenIncidentAndIsIdempotent()
    {
        Ctx c = Ctx.Create();
        CreateEmergencyEscalationResponse first = await c.Service.EscalateAsync(c.Rider.Id, c.Dispatch.Id, Request("ManualEscalation"), CancellationToken.None);
        CreateEmergencyEscalationResponse second = await c.Service.EscalateAsync(c.Rider.Id, c.Dispatch.Id, Request("ManualEscalation"), CancellationToken.None);
        first.Escalation.Id.Should().Be(second.Escalation.Id);
        c.Escalations.Items.Should().ContainSingle();
        c.Incidents.Items.Single().Status.Should().Be(IncidentStatus.Open);
        c.Dispatches.Items.Single().Status.Should().Be(AlertDispatchStatus.PendingDispatch);
        c.Audit.Actions.Should().Contain(AuditAction.EmergencyEscalationRequested);
    }

    [Fact]
    public async Task NonRiderOrForeignDispatchCannotCreateEscalation()
    {
        Ctx c = Ctx.Create();
        await Assert.ThrowsAsync<ForbiddenAppException>(() => c.Service.EscalateAsync(c.Monitor.Id, c.Dispatch.Id, Request("ManualEscalation"), CancellationToken.None));
        User other = User(UserRole.Rider); other.Id = "other-rider"; c.Users.Items.Add(other);
        await Assert.ThrowsAsync<NotFoundAppException>(() => c.Service.EscalateAsync(other.Id, c.Dispatch.Id, Request("ManualEscalation"), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenAppException>(() => c.Service.EscalateAsync(c.Admin.Id, c.Dispatch.Id, Request("ManualEscalation"), CancellationToken.None));
    }

    [Theory]
    [InlineData(IncidentStatus.Closed)]
    [InlineData(IncidentStatus.FalsePositiveCancelled)]
    public async Task ClosedOrCancelledIncidentCannotEscalate(IncidentStatus status)
    {
        Ctx c = Ctx.Create(incidentStatus: status);
        await Assert.ThrowsAsync<IncidentNotReadyAppException>(() => c.Service.EscalateAsync(c.Rider.Id, c.Dispatch.Id, Request("ManualEscalation"), CancellationToken.None));
    }

    [Fact]
    public async Task AcknowledgedBlocksEscalation()
    {
        Ctx c = Ctx.Create(acks: [Ack(AlertAcknowledgementStatus.Acknowledged)]);
        await Assert.ThrowsAsync<EmergencyEscalationNotAllowedAppException>(() => c.Service.EscalateAsync(c.Rider.Id, c.Dispatch.Id, Request("ManualEscalation"), CancellationToken.None));
    }

    [Fact]
    public async Task AllContactsDeclinedRequiresNonEmptyAllDeclined()
    {
        await Ctx.Create(acks: [Ack(AlertAcknowledgementStatus.Declined), Ack(AlertAcknowledgementStatus.Declined)]).Service.EscalateAsync("rider", "dispatch", Request("AllContactsDeclined"), CancellationToken.None);
        await Assert.ThrowsAsync<EmergencyEscalationNotAllowedAppException>(() => Ctx.Create().Service.EscalateAsync("rider", "dispatch", Request("AllContactsDeclined"), CancellationToken.None));
        await Assert.ThrowsAsync<EmergencyEscalationNotAllowedAppException>(() => Ctx.Create(acks: [Ack(AlertAcknowledgementStatus.Viewed)]).Service.EscalateAsync("rider", "dispatch", Request("AllContactsDeclined"), CancellationToken.None));
    }

    [Fact]
    public async Task NoAcknowledgementRequiresSimulatedSentAttempt()
    {
        await Ctx.Create(attempts: [Attempt(NotificationDeliveryStatus.SimulatedSent)]).Service.EscalateAsync("rider", "dispatch", Request("NoAcknowledgement"), CancellationToken.None);
        await Assert.ThrowsAsync<EmergencyEscalationNotAllowedAppException>(() => Ctx.Create().Service.EscalateAsync("rider", "dispatch", Request("NoAcknowledgement"), CancellationToken.None));
        await Assert.ThrowsAsync<EmergencyEscalationNotAllowedAppException>(() => Ctx.Create(attempts: [Attempt(NotificationDeliveryStatus.Prepared)]).Service.EscalateAsync("rider", "dispatch", Request("NoAcknowledgement"), CancellationToken.None));
    }

    [Fact]
    public async Task RiderAndMonitorCanGetStatusWhenOwnedOrAssigned()
    {
        Ctx c = Ctx.Create(attempts: [Attempt(NotificationDeliveryStatus.SimulatedSent)]);
        await c.Service.EscalateAsync(c.Rider.Id, c.Dispatch.Id, Request("NoAcknowledgement"), CancellationToken.None);
        (await c.Service.GetForRiderAsync(c.Rider.Id, c.Dispatch.Id, CancellationToken.None)).Escalation.AlertDispatchId.Should().Be(c.Dispatch.Id);
        (await c.Service.GetForMonitorAsync(c.Monitor.Id, c.Attempts.Items.Single().Id, CancellationToken.None)).Escalation.AlertDispatchId.Should().Be(c.Dispatch.Id);
        User otherMonitor = User(UserRole.Monitor); otherMonitor.Id = "other-monitor"; c.Users.Items.Add(otherMonitor);
        await Assert.ThrowsAsync<NotFoundAppException>(() => c.Service.GetForMonitorAsync(otherMonitor.Id, c.Attempts.Items.Single().Id, CancellationToken.None));
    }

    [Fact]
    public async Task MarkUnresolvedAndCancelAreIdempotentAndAudited()
    {
        Ctx c = Ctx.Create();
        await c.Service.EscalateAsync(c.Rider.Id, c.Dispatch.Id, Request("ManualEscalation"), CancellationToken.None);
        (await c.Service.MarkUnresolvedAsync(c.Rider.Id, c.Dispatch.Id, CancellationToken.None)).Escalation.Status.Should().Be("Unresolved");
        (await c.Service.MarkUnresolvedAsync(c.Rider.Id, c.Dispatch.Id, CancellationToken.None)).Escalation.Status.Should().Be("Unresolved");
        (await c.Service.CancelAsync(c.Rider.Id, c.Dispatch.Id, CancellationToken.None)).Escalation.Status.Should().Be("Cancelled");
        (await c.Service.CancelAsync(c.Rider.Id, c.Dispatch.Id, CancellationToken.None)).Escalation.Status.Should().Be("Cancelled");
        c.Audit.Actions.Should().Contain(AuditAction.EmergencyEscalationMarkedUnresolved).And.Contain(AuditAction.EmergencyEscalationCancelled);
    }

    [Fact]
    public async Task AuditFailureDoesNotBreakEscalationAndNoExternalEntitiesAreCreated()
    {
        Ctx c = Ctx.Create(audit: new FailingAudit());
        await c.Service.EscalateAsync(c.Rider.Id, c.Dispatch.Id, Request("ManualEscalation"), CancellationToken.None);
        c.Attempts.Items.Should().BeEmpty();
        c.Acks.Items.Should().BeEmpty();
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);
    private static CreateEmergencyEscalationRequest Request(string reason) => new(reason, "Level1", "notes");
    private static User User(UserRole role) => new() { Id = role.ToString().ToLowerInvariant(), Email = $"{role}@example.com", Role = role, IsActive = true };
    private static NotificationDeliveryAttempt Attempt(NotificationDeliveryStatus status) => new() { Id = Guid.NewGuid().ToString("N"), UserId = "rider", AlertDispatchId = "dispatch", IncidentId = "incident", TripId = "trip", EmergencyContactId = "contact", Status = status, CreatedAtUtc = Now, PreparedAtUtc = Now };
    private static AlertAcknowledgement Ack(AlertAcknowledgementStatus status) => new() { Id = Guid.NewGuid().ToString("N"), UserId = "rider", MonitorUserId = "monitor", AlertDispatchId = "dispatch", IncidentId = "incident", TripId = "trip", EmergencyContactId = "contact", Status = status, CreatedAtUtc = Now };

    private sealed class Ctx
    {
        public User Rider { get; private set; } = User(UserRole.Rider); public User Monitor { get; private set; } = User(UserRole.Monitor); public User Admin { get; private set; } = User(UserRole.Admin); public AlertDispatchRequest Dispatch { get; private set; } = new() { Id = "dispatch", UserId = "rider", IncidentId = "incident", TripId = "trip", Status = AlertDispatchStatus.PendingDispatch }; public Users Users { get; private set; } = null!; public Dispatches Dispatches { get; private set; } = null!; public Incidents Incidents { get; private set; } = null!; public Attempts Attempts { get; private set; } = null!; public Acks Acks { get; private set; } = null!; public Escalations Escalations { get; private set; } = null!; public Audit Audit { get; private set; } = null!; public EmergencyEscalationService Service { get; private set; } = null!;
        public static Ctx Create(IncidentStatus incidentStatus = IncidentStatus.Open, NotificationDeliveryAttempt[]? attempts = null, AlertAcknowledgement[]? acks = null, IAuditLogService? audit = null)
        {
            var c = new Ctx(); c.Users = new Users(c.Rider, c.Monitor, c.Admin); c.Dispatches = new Dispatches(c.Dispatch); c.Incidents = new Incidents(new Incident { Id = "incident", UserId = "rider", TripId = "trip", Status = incidentStatus, CreatedAtUtc = Now }); c.Attempts = new Attempts(attempts ?? []); c.Acks = new Acks(acks ?? []); c.Escalations = new Escalations(); c.Audit = audit as Audit ?? new Audit(); c.Service = new EmergencyEscalationService(c.Users, c.Dispatches, c.Incidents, c.Attempts, c.Acks, new Contacts(), c.Attempts, c.Escalations, new EmergencyEscalationIdempotencyKeyFactory(), new Clock(), audit ?? c.Audit); return c;
        }
    }
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => Now; }
    private sealed class Users(params User[] users) : IUserRepository { public List<User> Items { get; } = users.ToList(); public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Id == id)); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Email == email)); public Task AddAsync(User user, CancellationToken ct) { Items.Add(user); return Task.CompletedTask; } public Task UpdateAsync(User user, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Dispatches(params AlertDispatchRequest[] items) : IAlertDispatchRepository { public List<AlertDispatchRequest> Items { get; } = items.ToList(); public Task<AlertDispatchRequest?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id)); public Task<AlertDispatchRequest?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) => Task.FromResult<AlertDispatchRequest?>(null); public Task<(AlertDispatchRequest AlertDispatch, bool IsDuplicate)> AddOrGetDuplicateAsync(AlertDispatchRequest alertDispatch, CancellationToken ct) => throw new NotImplementedException(); public Task<IReadOnlyList<AlertDispatchRequest>> ListByUserIdAsync(string userId, AlertDispatchStatus? status, string? incidentId, int pageNumber, int pageSize, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertDispatchRequest>>([]); public Task<long> CountByUserIdAsync(string userId, AlertDispatchStatus? status, string? incidentId, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(AlertDispatchRequest alertDispatch, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Incidents(params Incident[] items) : IIncidentRepository { public List<Incident> Items { get; } = items.ToList(); public Task<Incident?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id)); public Task<Incident?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) => Task.FromResult<Incident?>(null); public Task<(Incident Incident, bool IsDuplicate)> AddOrGetDuplicateAsync(Incident incident, CancellationToken ct) => throw new NotImplementedException(); public Task<IReadOnlyList<Incident>> ListByUserIdAsync(string userId, IncidentStatus? status, string? tripId, int pageNumber, int pageSize, CancellationToken ct) => Task.FromResult<IReadOnlyList<Incident>>([]); public Task<long> CountByUserIdAsync(string userId, IncidentStatus? status, string? tripId, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(Incident incident, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Attempts(params NotificationDeliveryAttempt[] items) : INotificationDeliveryAttemptRepository, INotificationAttemptMonitorRepository { public List<NotificationDeliveryAttempt> Items { get; } = items.ToList(); public Task<NotificationDeliveryAttempt?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id)); public Task<NotificationDeliveryAttempt?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) => Task.FromResult<NotificationDeliveryAttempt?>(null); public Task<(NotificationDeliveryAttempt Attempt, bool IsDuplicate)> AddOrGetDuplicateAsync(NotificationDeliveryAttempt attempt, CancellationToken ct) => throw new NotImplementedException(); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByUserIdAsync(string userId, string? alertDispatchId, string? incidentId, NotificationDeliveryStatus? status, int pageNumber, int pageSize, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>(Items.Where(i => i.UserId == userId && (alertDispatchId is null || i.AlertDispatchId == alertDispatchId) && (incidentId is null || i.IncidentId == incidentId) && (!status.HasValue || i.Status == status)).ToArray()); public Task<long> CountByUserIdAsync(string userId, string? alertDispatchId, string? incidentId, NotificationDeliveryStatus? status, CancellationToken ct) => Task.FromResult(0L); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByEmergencyContactIdsAsync(IReadOnlyCollection<string> ids, int p, int s, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>(Items.Where(i => ids.Contains(i.EmergencyContactId)).ToArray()); public Task<long> CountByEmergencyContactIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(NotificationDeliveryAttempt attempt, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Acks(params AlertAcknowledgement[] items) : IAlertAcknowledgementRepository { public List<AlertAcknowledgement> Items { get; } = items.ToList(); public Task<AlertAcknowledgement?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id)); public Task<AlertAcknowledgement?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) => Task.FromResult<AlertAcknowledgement?>(null); public Task<(AlertAcknowledgement Acknowledgement, bool IsDuplicate)> AddOrGetDuplicateAsync(AlertAcknowledgement acknowledgement, CancellationToken ct) => throw new NotImplementedException(); public Task<IReadOnlyList<AlertAcknowledgement>> ListByMonitorUserIdAsync(string monitorUserId, AlertAcknowledgementStatus? status, int pageNumber, int pageSize, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertAcknowledgement>>([]); public Task<long> CountByMonitorUserIdAsync(string monitorUserId, AlertAcknowledgementStatus? status, CancellationToken ct) => Task.FromResult(0L); public Task<IReadOnlyList<AlertAcknowledgement>> ListByUserIdAsync(string userId, string? alertDispatchId, string? incidentId, AlertAcknowledgementStatus? status, int pageNumber, int pageSize, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertAcknowledgement>>(Items.Where(i => i.UserId == userId && (alertDispatchId is null || i.AlertDispatchId == alertDispatchId) && (incidentId is null || i.IncidentId == incidentId) && (!status.HasValue || i.Status == status)).ToArray()); public Task<long> CountByUserIdAsync(string userId, string? alertDispatchId, string? incidentId, AlertAcknowledgementStatus? status, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(AlertAcknowledgement acknowledgement, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Contacts : IMonitorLinkedContactRepository { public Task<IReadOnlyList<EmergencyContact>> GetActiveLinkedByLinkedUserIdAsync(string id, CancellationToken ct) => Task.FromResult<IReadOnlyList<EmergencyContact>>(id == "monitor" ? [new EmergencyContact { Id = "contact", LinkedUserId = id }] : []); }
    private sealed class Escalations : IEmergencyEscalationRepository { public List<EmergencyEscalation> Items { get; } = []; public Task<EmergencyEscalation?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id)); public Task<EmergencyEscalation?> GetByAlertDispatchIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.AlertDispatchId == id)); public Task<EmergencyEscalation?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.IdempotencyKey == key)); public Task<(EmergencyEscalation Escalation, bool IsDuplicate)> AddOrGetDuplicateAsync(EmergencyEscalation e, CancellationToken ct) { EmergencyEscalation? existing = Items.FirstOrDefault(i => i.AlertDispatchId == e.AlertDispatchId || i.IdempotencyKey == e.IdempotencyKey); if (existing is not null) return Task.FromResult((existing, true)); Items.Add(e); return Task.FromResult((e, false)); } public Task<IReadOnlyList<EmergencyEscalation>> ListAsync(EmergencyEscalationQuery query, CancellationToken ct) => Task.FromResult<IReadOnlyList<EmergencyEscalation>>(Items); public Task<long> CountAsync(EmergencyEscalationQuery query, CancellationToken ct) => Task.FromResult((long)Items.Count); public Task UpdateAsync(EmergencyEscalation escalation, CancellationToken ct) => Task.CompletedTask; }
    private class Audit : IAuditLogService { public List<AuditAction> Actions { get; } = []; public virtual Task RecordAsync(string actorUserId, string actorRole, AuditAction action, AuditModule module, string entityType, string? entityId, AuditOutcome outcome, string? reason, string? requestPath, string? httpMethod, IReadOnlyDictionary<string, string>? metadata, CancellationToken cancellationToken) { Actions.Add(action); return Task.CompletedTask; } public Task<GetAuditLogsResponse> ListAsync(string adminUserId, AuditLogQuery query, CancellationToken cancellationToken) => throw new NotImplementedException(); public Task<AuditLogResponse> GetAsync(string adminUserId, string id, CancellationToken cancellationToken) => throw new NotImplementedException(); }
    private sealed class FailingAudit : Audit { public override Task RecordAsync(string actorUserId, string actorRole, AuditAction action, AuditModule module, string entityType, string? entityId, AuditOutcome outcome, string? reason, string? requestPath, string? httpMethod, IReadOnlyDictionary<string, string>? metadata, CancellationToken cancellationToken) => throw new InvalidOperationException("audit failed"); }
}
