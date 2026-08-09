using FluentAssertions;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AlertAcknowledgements.Application;
using MotoSOS.API.Modules.AlertDispatch.Application;
using MotoSOS.API.Modules.AlertDispatch.Domain;
using MotoSOS.API.Modules.AuditLogs.Application;
using MotoSOS.API.Modules.AuditLogs.Contracts;
using MotoSOS.API.Modules.AuditLogs.Domain;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
using MotoSOS.API.Modules.EmergencyResolution.Application;
using MotoSOS.API.Modules.EmergencyResolution.Domain;
using MotoSOS.API.Modules.EvidenceAttachments.Application;
using MotoSOS.API.Modules.EvidenceAttachments.Contracts;
using MotoSOS.API.Modules.EvidenceAttachments.Domain;
using MotoSOS.API.Modules.Incidents.Application;
using MotoSOS.API.Modules.Incidents.Domain;
using MotoSOS.API.Modules.Notifications.Application;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace UnitTest.EvidenceAttachments;

public sealed class EvidenceAttachmentServiceTests
{
    [Fact]
    public async Task RiderRegistersOwnIncidentEvidenceAndSanitizesMetadata()
    {
        Ctx c = Ctx.Create();
        CreateEvidenceAttachmentResponse response = await c.Service.CreateForRiderAsync("rider", Request(), CancellationToken.None);

        response.EvidenceAttachment.UserId.Should().Be("rider");
        response.EvidenceAttachment.RegisteredByUserId.Should().Be("rider");
        response.EvidenceAttachment.RegisteredByRole.Should().Be("Rider");
        response.EvidenceAttachment.TargetType.Should().Be("Incident");
        response.EvidenceAttachment.Metadata.Should().ContainKey("camera");
        response.EvidenceAttachment.Metadata.Should().NotContainKey("access" + "Token");
        response.EvidenceAttachment.Metadata["long"].Should().HaveLength(200);
        c.Evidence.Items.Should().ContainSingle();
        c.Incidents.Items.Single().Status.Should().Be(IncidentStatus.Open);
        c.Alerts.Items.Single().Status.Should().Be(AlertDispatchStatus.PendingDispatch);
        c.Reports.Items.Single().Summary.Should().Be("done");
    }

    [Fact]
    public async Task RiderCanRegisterAlertAndReportButCannotRegisterForeignTarget()
    {
        Ctx c = Ctx.Create();
        (await c.Service.CreateForRiderAsync("rider", Request(incidentId: null, alertDispatchId: "alert"), CancellationToken.None)).EvidenceAttachment.TargetType.Should().Be("AlertDispatch");
        (await c.Service.CreateForRiderAsync("rider", Request(incidentId: null, reportId: "report"), CancellationToken.None)).EvidenceAttachment.TargetType.Should().Be("EmergencyResolutionReport");
        await Assert.ThrowsAsync<NotFoundAppException>(() => c.Service.CreateForRiderAsync("other", Request(), CancellationToken.None));
    }

    [Fact]
    public async Task MonitorRegistersOnlyWhenAssignedAndAdminCannotRegister()
    {
        Ctx c = Ctx.Create();
        CreateEvidenceAttachmentResponse response = await c.Service.CreateForMonitorAsync("monitor", Request(incidentId: null, alertDispatchId: "alert"), CancellationToken.None);

        response.EvidenceAttachment.UserId.Should().Be("rider");
        response.EvidenceAttachment.RegisteredByUserId.Should().Be("monitor");
        response.EvidenceAttachment.RegisteredByRole.Should().Be("Monitor");
        await Assert.ThrowsAsync<NotFoundAppException>(() => Ctx.Create(assigned: false).Service.CreateForMonitorAsync("monitor", Request(incidentId: null, alertDispatchId: "alert"), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenAppException>(() => c.Service.CreateForRiderAsync("admin", Request(), CancellationToken.None));
    }

    [Fact]
    public async Task DuplicateCreateReturnsExistingAndDoesNotUpdateTimestamp()
    {
        Ctx c = Ctx.Create();
        CreateEvidenceAttachmentResponse first = await c.Service.CreateForRiderAsync("rider", Request(), CancellationToken.None);
        CreateEvidenceAttachmentResponse second = await c.Service.CreateForRiderAsync("rider", Request(description: "changed"), CancellationToken.None);

        second.EvidenceAttachment.Id.Should().Be(first.EvidenceAttachment.Id);
        second.EvidenceAttachment.Description.Should().Be(first.EvidenceAttachment.Description);
        second.EvidenceAttachment.UpdatedAtUtc.Should().Be(first.EvidenceAttachment.UpdatedAtUtc);
        c.Evidence.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task RiderListGetDeleteAndAdminReadWorkAsExpected()
    {
        Ctx c = Ctx.Create();
        string id = (await c.Service.CreateForRiderAsync("rider", Request(), CancellationToken.None)).EvidenceAttachment.Id;
        (await c.Service.ListForRiderAsync("rider", new EvidenceAttachmentQuery(null, null, null, null, null, null, null, null, null, 1, 20), CancellationToken.None)).TotalCount.Should().Be(1);
        (await c.Service.GetForRiderAsync("rider", id, CancellationToken.None)).Id.Should().Be(id);
        await Assert.ThrowsAsync<EvidenceAttachmentNotAvailableAppException>(() => c.Service.GetForRiderAsync("other", id, CancellationToken.None));

        EvidenceAttachmentResponse deleted = await c.Service.DeleteForRiderAsync("rider", id, CancellationToken.None);
        deleted.Status.Should().Be("MarkedDeleted");
        (await c.Service.DeleteForRiderAsync("rider", id, CancellationToken.None)).DeletedAtUtc.Should().Be(deleted.DeletedAtUtc);
        (await c.Service.ListForRiderAsync("rider", new EvidenceAttachmentQuery(null, null, null, null, null, null, null, null, null, 1, 20), CancellationToken.None)).TotalCount.Should().Be(0);
        (await c.Service.ListForAdminAsync("admin", new EvidenceAttachmentQuery(null, null, null, null, null, null, null, null, null, 1, 20), CancellationToken.None)).TotalCount.Should().Be(1);
        (await c.Service.GetForAdminAsync("admin", id, CancellationToken.None)).Id.Should().Be(id);
    }

    [Fact]
    public async Task AuditFailureDoesNotBreakCreateOrDelete()
    {
        Ctx c = Ctx.Create(audit: new FailingAudit());
        string id = (await c.Service.CreateForRiderAsync("rider", Request(), CancellationToken.None)).EvidenceAttachment.Id;
        (await c.Service.DeleteForRiderAsync("rider", id, CancellationToken.None)).Status.Should().Be("MarkedDeleted");
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);
    private static CreateEvidenceAttachmentRequest Request(string? incidentId = "incident", string? alertDispatchId = null, string? reportId = null, string? description = "safe") => new(incidentId, alertDispatchId, reportId, "client", "Photo", "RiderMobileApp", "evidence.jpg", "image/jpeg", 10, new string('a', 64), "local://evidence/evidence.jpg", "None", description, Now.AddMinutes(-1), new Dictionary<string, string> { ["camera"] = "rear", ["access" + "Token"] = "hidden", ["long"] = new string('x', 250) });
    private sealed class Ctx { public Incidents Incidents { get; private set; } = null!; public Alerts Alerts { get; private set; } = null!; public Reports Reports { get; private set; } = null!; public Evidence Evidence { get; private set; } = null!; public EvidenceAttachmentService Service { get; private set; } = null!; public static Ctx Create(bool assigned = true, IAuditLogService? audit = null) { var c = new Ctx(); c.Incidents = new Incidents(new Incident { Id = "incident", UserId = "rider", TripId = "trip", Status = IncidentStatus.Open }); c.Alerts = new Alerts(new AlertDispatchRequest { Id = "alert", UserId = "rider", IncidentId = "incident", TripId = "trip", Status = AlertDispatchStatus.PendingDispatch }); c.Reports = new Reports(new EmergencyResolutionReport { Id = "report", UserId = "rider", IncidentId = "incident", AlertDispatchId = "alert", TripId = "trip", Summary = "done" }); c.Evidence = new Evidence(); var attempts = new Attempts(new NotificationDeliveryAttempt { Id = "attempt", UserId = "rider", AlertDispatchId = "alert", IncidentId = "incident", TripId = "trip", EmergencyContactId = "contact" }); var contacts = new Contacts(assigned ? [new EmergencyContact { Id = "contact", LinkedUserId = "monitor", IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Linked }] : []); c.Service = new EvidenceAttachmentService(new Users(), c.Incidents, c.Alerts, c.Reports, attempts, contacts, c.Evidence, new EvidenceAttachmentIdempotencyKeyFactory(), new Clock(), audit ?? new Audit()); return c; } }
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => Now; }
    private sealed class Users : IUserRepository { public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult<User?>(id switch { "rider" => new User { Id = id, Role = UserRole.Rider, IsActive = true }, "other" => new User { Id = id, Role = UserRole.Rider, IsActive = true }, "monitor" => new User { Id = id, Role = UserRole.Monitor, IsActive = true }, "admin" => new User { Id = id, Role = UserRole.Admin, IsActive = true }, _ => null }); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult<User?>(null); public Task AddAsync(User user, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(User user, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Incidents(params Incident[] items) : IIncidentRepository { public List<Incident> Items { get; } = items.ToList(); public Task<Incident?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id)); public Task<Incident?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) => Task.FromResult<Incident?>(null); public Task<(Incident Incident, bool IsDuplicate)> AddOrGetDuplicateAsync(Incident incident, CancellationToken ct) => throw new NotImplementedException(); public Task<IReadOnlyList<Incident>> ListByUserIdAsync(string userId, IncidentStatus? status, string? tripId, int pageNumber, int pageSize, CancellationToken ct) => Task.FromResult<IReadOnlyList<Incident>>([]); public Task<long> CountByUserIdAsync(string userId, IncidentStatus? status, string? tripId, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(Incident incident, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Alerts(params AlertDispatchRequest[] items) : IAlertDispatchRepository { public List<AlertDispatchRequest> Items { get; } = items.ToList(); public Task<AlertDispatchRequest?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.Id == id)); public Task<AlertDispatchRequest?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) => Task.FromResult<AlertDispatchRequest?>(null); public Task<(AlertDispatchRequest AlertDispatch, bool IsDuplicate)> AddOrGetDuplicateAsync(AlertDispatchRequest alertDispatch, CancellationToken ct) => throw new NotImplementedException(); public Task<IReadOnlyList<AlertDispatchRequest>> ListByUserIdAsync(string userId, AlertDispatchStatus? status, string? incidentId, int pageNumber, int pageSize, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertDispatchRequest>>([]); public Task<long> CountByUserIdAsync(string userId, AlertDispatchStatus? status, string? incidentId, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(AlertDispatchRequest alertDispatch, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Reports(params EmergencyResolutionReport[] items) : IEmergencyResolutionRepository { public List<EmergencyResolutionReport> Items { get; } = items.ToList(); public Task<EmergencyResolutionReport?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(r => r.Id == id)); public Task<EmergencyResolutionReport?> GetByIncidentIdAsync(string incidentId, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(r => r.IncidentId == incidentId)); public Task<EmergencyResolutionReport?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) => Task.FromResult<EmergencyResolutionReport?>(null); public Task<(EmergencyResolutionReport Report, bool IsDuplicate)> AddOrGetDuplicateAsync(EmergencyResolutionReport report, CancellationToken ct) => throw new NotImplementedException(); public Task<IReadOnlyList<EmergencyResolutionReport>> ListByUserIdAsync(string userId, EmergencyResolutionOutcome? outcome, int pageNumber, int pageSize, CancellationToken ct) => Task.FromResult<IReadOnlyList<EmergencyResolutionReport>>([]); public Task<long> CountByUserIdAsync(string userId, EmergencyResolutionOutcome? outcome, CancellationToken ct) => Task.FromResult(0L); }
    private sealed class Attempts(params NotificationDeliveryAttempt[] items) : INotificationDeliveryAttemptRepository { public List<NotificationDeliveryAttempt> Items { get; } = items.ToList(); public Task<NotificationDeliveryAttempt?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.Id == id)); public Task<NotificationDeliveryAttempt?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) => Task.FromResult<NotificationDeliveryAttempt?>(null); public Task<(NotificationDeliveryAttempt Attempt, bool IsDuplicate)> AddOrGetDuplicateAsync(NotificationDeliveryAttempt attempt, CancellationToken ct) => throw new NotImplementedException(); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByUserIdAsync(string userId, string? alertDispatchId, string? incidentId, NotificationDeliveryStatus? status, int pageNumber, int pageSize, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>(Items.Where(a => a.UserId == userId && (alertDispatchId is null || a.AlertDispatchId == alertDispatchId) && (incidentId is null || a.IncidentId == incidentId)).ToArray()); public Task<long> CountByUserIdAsync(string userId, string? alertDispatchId, string? incidentId, NotificationDeliveryStatus? status, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(NotificationDeliveryAttempt attempt, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Contacts(EmergencyContact[] items) : IMonitorLinkedContactRepository { public Task<IReadOnlyList<EmergencyContact>> GetActiveLinkedByLinkedUserIdAsync(string linkedUserId, CancellationToken ct) => Task.FromResult<IReadOnlyList<EmergencyContact>>(items.Where(c => c.LinkedUserId == linkedUserId && c.IsActive && c.InvitationStatus == EmergencyContactInvitationStatus.Linked).ToArray()); }
    private sealed class Evidence : IEvidenceAttachmentRepository { public List<EvidenceAttachment> Items { get; } = []; public Task<EvidenceAttachment?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(e => e.Id == id)); public Task<EvidenceAttachment?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(e => e.IdempotencyKey == idempotencyKey)); public Task<(EvidenceAttachment EvidenceAttachment, bool IsDuplicate)> AddOrGetDuplicateAsync(EvidenceAttachment evidenceAttachment, CancellationToken ct) { EvidenceAttachment? existing = Items.FirstOrDefault(e => e.IdempotencyKey == evidenceAttachment.IdempotencyKey); if (existing is not null) return Task.FromResult((existing, true)); Items.Add(evidenceAttachment); return Task.FromResult((evidenceAttachment, false)); } public Task UpdateAsync(EvidenceAttachment evidenceAttachment, CancellationToken ct) => Task.CompletedTask; public Task<IReadOnlyList<EvidenceAttachment>> ListByUserIdAsync(string userId, EvidenceAttachmentQuery query, CancellationToken ct) => Task.FromResult<IReadOnlyList<EvidenceAttachment>>(Items.Where(e => e.UserId == userId && (!query.Status.HasValue || e.Status == query.Status)).ToArray()); public Task<long> CountByUserIdAsync(string userId, EvidenceAttachmentQuery query, CancellationToken ct) => Task.FromResult((long)Items.Count(e => e.UserId == userId && (!query.Status.HasValue || e.Status == query.Status))); public Task<IReadOnlyList<EvidenceAttachment>> ListAsync(EvidenceAttachmentQuery query, CancellationToken ct) => Task.FromResult<IReadOnlyList<EvidenceAttachment>>(Items); public Task<long> CountAsync(EvidenceAttachmentQuery query, CancellationToken ct) => Task.FromResult((long)Items.Count); }
    private class Audit : IAuditLogService { public Task RecordAsync(string actorUserId, string actorRole, AuditAction action, AuditModule module, string entityType, string? entityId, AuditOutcome outcome, string? reason, string? requestPath, string? httpMethod, IReadOnlyDictionary<string, string>? metadata, CancellationToken cancellationToken) => Task.CompletedTask; public Task<GetAuditLogsResponse> ListAsync(string adminUserId, AuditLogQuery query, CancellationToken cancellationToken) => throw new NotImplementedException(); public Task<AuditLogResponse> GetAsync(string adminUserId, string id, CancellationToken cancellationToken) => throw new NotImplementedException(); }
    private sealed class FailingAudit : Audit { public new Task RecordAsync(string actorUserId, string actorRole, AuditAction action, AuditModule module, string entityType, string? entityId, AuditOutcome outcome, string? reason, string? requestPath, string? httpMethod, IReadOnlyDictionary<string, string>? metadata, CancellationToken cancellationToken) => throw new InvalidOperationException("audit failed"); }
}
