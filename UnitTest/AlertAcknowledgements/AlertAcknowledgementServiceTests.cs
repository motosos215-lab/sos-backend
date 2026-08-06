using FluentAssertions;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AlertAcknowledgements.Application;
using MotoSOS.API.Modules.AlertAcknowledgements.Contracts;
using MotoSOS.API.Modules.AlertAcknowledgements.Domain;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace UnitTest.AlertAcknowledgements;

public sealed class AlertAcknowledgementServiceTests
{
    [Fact]
    public async Task MonitorCanListViewAcknowledgeAndDeclineAssignedAlertsOnly()
    {
        User monitor = User(UserRole.Monitor); var contacts = new Contacts(Contact("c1", monitor.Id)); NotificationDeliveryAttempt attempt = Attempt("rider", "c1"); var acks = new Acks(); AlertAcknowledgementService service = Service(monitor, contacts, new Attempts(attempt), acks);
        (await service.ListMonitorAlertsAsync(monitor.Id, null, null, null, CancellationToken.None)).Alerts.Should().ContainSingle();
        (await service.GetMonitorAlertAsync(monitor.Id, attempt.Id, CancellationToken.None)).Acknowledgement.Status.Should().Be("Pending");
        (await service.ViewAsync(monitor.Id, attempt.Id, CancellationToken.None)).Acknowledgement.Status.Should().Be("Viewed");
        AcknowledgeAlertResponse acknowledged = await service.AcknowledgeAsync(monitor.Id, attempt.Id, new AcknowledgeAlertRequest("CanAssist", "ok"), CancellationToken.None);
        acknowledged.Acknowledgement.Status.Should().Be("Acknowledged");
        (await service.AcknowledgeAsync(monitor.Id, attempt.Id, new AcknowledgeAlertRequest("CanAssist", "ok"), CancellationToken.None)).Acknowledgement.Status.Should().Be("Acknowledged");
        await Assert.ThrowsAsync<NotFoundAppException>(() => service.GetMonitorAlertAsync(monitor.Id, Attempt("rider", "other").Id, CancellationToken.None));
    }

    [Fact]
    public async Task DeclineAndInvalidTransitionsFollowRules()
    {
        User monitor = User(UserRole.Monitor); NotificationDeliveryAttempt attempt = Attempt("rider", "c1"); AlertAcknowledgementService service = Service(monitor, new Contacts(Contact("c1", monitor.Id)), new Attempts(attempt), new Acks());
        (await service.DeclineAsync(monitor.Id, attempt.Id, new DeclineAlertRequest("CannotAssist", "no"), CancellationToken.None)).Acknowledgement.Status.Should().Be("Declined");
        (await service.DeclineAsync(monitor.Id, attempt.Id, new DeclineAlertRequest("CannotAssist", "no"), CancellationToken.None)).Acknowledgement.Status.Should().Be("Declined");
        await Assert.ThrowsAsync<AcknowledgementAlreadyDeclinedAppException>(() => service.AcknowledgeAsync(monitor.Id, attempt.Id, new AcknowledgeAlertRequest("CanAssist", null), CancellationToken.None));
        NotificationDeliveryAttempt attempt2 = Attempt("rider", "c1"); AlertAcknowledgementService service2 = Service(monitor, new Contacts(Contact("c1", monitor.Id)), new Attempts(attempt2), new Acks());
        await service2.AcknowledgeAsync(monitor.Id, attempt2.Id, new AcknowledgeAlertRequest("CanAssist", null), CancellationToken.None);
        await Assert.ThrowsAsync<AcknowledgementAlreadyConfirmedAppException>(() => service2.DeclineAsync(monitor.Id, attempt2.Id, new DeclineAlertRequest("CannotAssist", null), CancellationToken.None));
    }

    [Fact]
    public async Task RiderCanListOwnAcknowledgementsButCannotRespondAndAdminForbidden()
    {
        User rider = User(UserRole.Rider); AlertAcknowledgement ack = Ack(rider.Id, "monitor"); AlertAcknowledgementService riderService = Service(rider, new Contacts(), new Attempts(), new Acks(ack));
        (await riderService.ListRiderAcknowledgementsAsync(rider.Id, null, null, null, null, null, CancellationToken.None)).Acknowledgements.Should().ContainSingle();
        await Assert.ThrowsAsync<ForbiddenAppException>(() => riderService.ViewAsync(rider.Id, "attempt", CancellationToken.None));
        User admin = User(UserRole.Admin); await Assert.ThrowsAsync<ForbiddenAppException>(() => Service(admin, new Contacts(), new Attempts(), new Acks()).ListMonitorAlertsAsync(admin.Id, null, null, null, CancellationToken.None));
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 6, 11, 0, 0, TimeSpan.Zero);
    private static AlertAcknowledgementService Service(User user, Contacts contacts, Attempts attempts, Acks acks) => new(new Users(user), contacts, attempts, acks, new AlertAcknowledgementIdempotencyKeyFactory(), new Clock());
    private static User User(UserRole role) => new() { Email = $"{Guid.NewGuid()}@example.com", Role = role, IsActive = true, FullName = "User" };
    private static EmergencyContact Contact(string id, string monitorId) => new() { Id = id, UserId = "rider", LinkedUserId = monitorId, IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Linked };
    private static NotificationDeliveryAttempt Attempt(string riderId, string contactId) => new() { UserId = riderId, EmergencyContactId = contactId, AlertDispatchId = "alert", IncidentId = "incident", TripId = "trip", Channel = NotificationChannel.Sms, Status = NotificationDeliveryStatus.Prepared, Provider = NotificationProvider.None, PreparedAtUtc = Now, CreatedAtUtc = Now };
    private static AlertAcknowledgement Ack(string userId, string monitorId) => new() { UserId = userId, MonitorUserId = monitorId, EmergencyContactId = "contact", AlertDispatchId = "alert", NotificationDeliveryAttemptId = "attempt", IncidentId = "incident", TripId = "trip", Status = AlertAcknowledgementStatus.Acknowledged, ResponseType = AlertAcknowledgementResponseType.CanAssist, CreatedAtUtc = Now, IdempotencyKey = Guid.NewGuid().ToString() };
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => Now; }
    private sealed class Users(User user) : IUserRepository { public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult<User?>(user.Id == id ? user : null); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult<User?>(null); public Task AddAsync(User u, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(User u, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Contacts(params EmergencyContact[] contacts) : IMonitorLinkedContactRepository { public Task<IReadOnlyList<EmergencyContact>> GetActiveLinkedByLinkedUserIdAsync(string id, CancellationToken ct) => Task.FromResult<IReadOnlyList<EmergencyContact>>(contacts.Where(c => c.LinkedUserId == id && c.IsActive && c.InvitationStatus == EmergencyContactInvitationStatus.Linked).ToArray()); }
    private sealed class Attempts(params NotificationDeliveryAttempt[] attempts) : INotificationAttemptMonitorRepository { private readonly List<NotificationDeliveryAttempt> _items = attempts.ToList(); public Task<NotificationDeliveryAttempt?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(_items.FirstOrDefault(a => a.Id == id)); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByEmergencyContactIdsAsync(IReadOnlyCollection<string> ids, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>(_items.Where(a => ids.Contains(a.EmergencyContactId)).ToArray()); public Task<long> CountByEmergencyContactIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct) => Task.FromResult((long)_items.Count(a => ids.Contains(a.EmergencyContactId))); }
    private sealed class Acks(params AlertAcknowledgement[] acks) : IAlertAcknowledgementRepository { private readonly List<AlertAcknowledgement> _items = acks.ToList(); public Task<AlertAcknowledgement?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(_items.FirstOrDefault(a => a.Id == id)); public Task<AlertAcknowledgement?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult(_items.FirstOrDefault(a => a.IdempotencyKey == key)); public Task<(AlertAcknowledgement Acknowledgement, bool IsDuplicate)> AddOrGetDuplicateAsync(AlertAcknowledgement ack, CancellationToken ct) { AlertAcknowledgement? existing = _items.FirstOrDefault(a => a.IdempotencyKey == ack.IdempotencyKey); if (existing is not null) return Task.FromResult((existing, true)); _items.Add(ack); return Task.FromResult((ack, false)); } public Task<IReadOnlyList<AlertAcknowledgement>> ListByMonitorUserIdAsync(string id, AlertAcknowledgementStatus? s, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertAcknowledgement>>(_items.Where(a => a.MonitorUserId == id && (!s.HasValue || a.Status == s)).ToArray()); public Task<long> CountByMonitorUserIdAsync(string id, AlertAcknowledgementStatus? s, CancellationToken ct) => Task.FromResult((long)_items.Count(a => a.MonitorUserId == id)); public Task<IReadOnlyList<AlertAcknowledgement>> ListByUserIdAsync(string id, string? ad, string? i, AlertAcknowledgementStatus? s, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertAcknowledgement>>(_items.Where(a => a.UserId == id).ToArray()); public Task<long> CountByUserIdAsync(string id, string? ad, string? i, AlertAcknowledgementStatus? s, CancellationToken ct) => Task.FromResult((long)_items.Count(a => a.UserId == id)); public Task UpdateAsync(AlertAcknowledgement ack, CancellationToken ct) => Task.CompletedTask; }
}
