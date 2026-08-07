using FluentAssertions;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AlertAcknowledgements.Application;
using MotoSOS.API.Modules.AlertAcknowledgements.Domain;
using MotoSOS.API.Modules.AlertDispatch.Application;
using MotoSOS.API.Modules.AlertDispatch.Domain;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
using MotoSOS.API.Modules.EmergencyStatus.Application;
using MotoSOS.API.Modules.EmergencyStatus.Contracts;
using MotoSOS.API.Modules.Incidents.Application;
using MotoSOS.API.Modules.Incidents.Domain;
using MotoSOS.API.Modules.LocationSharing.Application;
using MotoSOS.API.Modules.LocationSharing.Domain;
using MotoSOS.API.Modules.Notifications.Application;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.Trips.Application;
using MotoSOS.API.Modules.Trips.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace UnitTest.EmergencyStatus;

public sealed class EmergencyStatusServiceTests
{
    [Fact]
    public async Task RiderCanGetOwnEmergencyStatusWithScopedCountsAndLocation()
    {
        User rider = User(UserRole.Rider); Incident incident = Incident(rider.Id); Trip trip = Trip(rider.Id); AlertDispatchRequest dispatch = Dispatch(rider.Id, incident.Id); var locations = new Locations(Snapshot(incident, stale: false));
        var service = Service(rider, new Incidents(incident), new Trips(trip), new Alerts(dispatch), new Attempts(Attempt(rider.Id, incident.Id, dispatch.Id, NotificationDeliveryStatus.Prepared), Attempt(rider.Id, "other", "other-alert", NotificationDeliveryStatus.Failed)), new Acks(Ack(rider.Id, incident.Id, dispatch.Id, AlertAcknowledgementStatus.Acknowledged), Ack(rider.Id, "other", "other-alert", AlertAcknowledgementStatus.Declined)), locations);

        EmergencyStatusResponse response = await service.GetForRiderAsync(rider.Id, incident.Id, CancellationToken.None);

        response.Incident.Id.Should().Be(incident.Id);
        response.Trip!.Id.Should().Be(trip.Id);
        response.AlertDispatch!.Id.Should().Be(dispatch.Id);
        response.Notifications.Total.Should().Be(1);
        response.Acknowledgements.Acknowledged.Should().Be(1);
        response.Location.Available.Should().BeTrue();
        response.Location.IsStale.Should().BeFalse();
        response.OverallStatus.Should().Be("Acknowledged");
        response.RequiresAttention.Should().BeFalse();
        response.LastUpdatedAtUtc.Should().Be(Now.AddMinutes(5));
    }

    [Fact]
    public async Task RiderCannotGetForeignEmergencyAndAdminIsForbidden()
    {
        User rider = User(UserRole.Rider); User admin = User(UserRole.Admin); Incident incident = Incident("owner");
        await Assert.ThrowsAsync<NotFoundAppException>(() => Service(rider, new Incidents(incident)).GetForRiderAsync(rider.Id, incident.Id, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenAppException>(() => Service(admin, new Incidents(incident)).GetForRiderAsync(admin.Id, incident.Id, CancellationToken.None));
    }

    [Fact]
    public async Task MissingComplementaryDataReturnsNullsAndZeroCounts()
    {
        User rider = User(UserRole.Rider); Incident incident = Incident(rider.Id);
        EmergencyStatusResponse response = await Service(rider, new Incidents(incident)).GetForRiderAsync(rider.Id, incident.Id, CancellationToken.None);
        response.AlertDispatch.Should().BeNull(); response.Trip.Should().BeNull(); response.Notifications.Total.Should().Be(0); response.Acknowledgements.Total.Should().Be(0); response.Location.Available.Should().BeFalse(); response.OverallStatus.Should().Be("Active"); response.RequiresAttention.Should().BeTrue();
    }

    [Fact]
    public async Task MonitorCanGetAssignedAlertOnly()
    {
        User monitor = User(UserRole.Monitor); Incident incident = Incident("rider"); NotificationDeliveryAttempt attempt = Attempt("rider", incident.Id, "alert", NotificationDeliveryStatus.Prepared, contactId: "contact-1");
        var service = Service(monitor, new Incidents(incident), attempts: new Attempts(attempt), contacts: new Contacts(Contact("contact-1", monitor.Id)));
        (await service.GetForMonitorAsync(monitor.Id, attempt.Id, CancellationToken.None)).Incident.Id.Should().Be(incident.Id);
        await Assert.ThrowsAsync<NotFoundAppException>(() => Service(monitor, new Incidents(incident), attempts: new Attempts(attempt), contacts: new Contacts(Contact("other", monitor.Id))).GetForMonitorAsync(monitor.Id, attempt.Id, CancellationToken.None));
    }

    [Theory]
    [InlineData(IncidentStatus.Closed, null, "Closed", false)]
    [InlineData(IncidentStatus.FalsePositiveCancelled, null, "Cancelled", false)]
    [InlineData(IncidentStatus.Open, AlertAcknowledgementStatus.Declined, "Declined", true)]
    [InlineData(IncidentStatus.Open, null, "AwaitingAcknowledgement", true)]
    public async Task OverallStatusFollowsRules(IncidentStatus incidentStatus, AlertAcknowledgementStatus? ackStatus, string expected, bool requiresAttention)
    {
        User rider = User(UserRole.Rider); Incident incident = Incident(rider.Id, incidentStatus); AlertDispatchRequest dispatch = Dispatch(rider.Id, incident.Id); var acks = ackStatus.HasValue ? new Acks(Ack(rider.Id, incident.Id, dispatch.Id, ackStatus.Value)) : new Acks();
        EmergencyStatusResponse response = await Service(rider, new Incidents(incident), alerts: new Alerts(dispatch), attempts: new Attempts(Attempt(rider.Id, incident.Id, dispatch.Id, NotificationDeliveryStatus.Prepared)), acks: acks).GetForRiderAsync(rider.Id, incident.Id, CancellationToken.None);
        response.OverallStatus.Should().Be(expected); response.RequiresAttention.Should().Be(requiresAttention);
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 6, 14, 0, 0, TimeSpan.Zero);
    private static EmergencyStatusService Service(User user, Incidents? incidents = null, Trips? trips = null, Alerts? alerts = null, Attempts? attempts = null, Acks? acks = null, Locations? locations = null, Contacts? contacts = null) => new(new Users(user), incidents ?? new Incidents(), trips ?? new Trips(), alerts ?? new Alerts(), attempts ?? new Attempts(), acks ?? new Acks(), locations ?? new Locations(), contacts ?? new Contacts(), attempts ?? new Attempts(), new LocationSharingStalenessService(), new Clock());
    private static User User(UserRole role) => new() { Email = $"{Guid.NewGuid()}@example.com", FullName = "User", Role = role, IsActive = true };
    private static Incident Incident(string userId, IncidentStatus status = IncidentStatus.Open) => new() { Id = "incident", UserId = userId, TripId = "trip", Source = IncidentSource.MobileDetection, Cause = IncidentCause.CountdownTimeout, RiskLevel = IncidentRiskLevel.High, Status = status, OccurredAtUtc = Now, CreatedAtUtc = Now, UpdatedAtUtc = Now.AddMinutes(1) };
    private static Trip Trip(string userId) => new() { Id = "trip", UserId = userId, Status = TripStatus.Active, StartedAtUtc = Now.AddMinutes(-20), CreatedAtUtc = Now.AddMinutes(-20), UpdatedAtUtc = Now.AddMinutes(2) };
    private static AlertDispatchRequest Dispatch(string userId, string incidentId) => new() { Id = "alert", UserId = userId, IncidentId = incidentId, TripId = "trip", Priority = AlertDispatchPriority.High, Reason = AlertDispatchReason.IncidentCreated, Status = AlertDispatchStatus.PendingDispatch, CreatedAtUtc = Now.AddMinutes(3), UpdatedAtUtc = Now.AddMinutes(3) };
    private static NotificationDeliveryAttempt Attempt(string userId, string incidentId, string alertId, NotificationDeliveryStatus status, string contactId = "contact") => new() { UserId = userId, IncidentId = incidentId, AlertDispatchId = alertId, TripId = "trip", EmergencyContactId = contactId, Status = status, Channel = NotificationChannel.Sms, Provider = NotificationProvider.None, PreparedAtUtc = Now, CreatedAtUtc = Now, UpdatedAtUtc = Now.AddMinutes(4) };
    private static AlertAcknowledgement Ack(string userId, string incidentId, string alertId, AlertAcknowledgementStatus status) => new() { UserId = userId, MonitorUserId = "monitor", IncidentId = incidentId, AlertDispatchId = alertId, NotificationDeliveryAttemptId = "attempt", EmergencyContactId = "contact", TripId = "trip", Status = status, CreatedAtUtc = Now, UpdatedAtUtc = Now.AddMinutes(5) };
    private static EmergencyLocationSnapshot Snapshot(Incident incident, bool stale) => new() { UserId = incident.UserId, IncidentId = incident.Id, TripId = incident.TripId, Latitude = 19, Longitude = -99, Source = LocationSharingSource.MobileApp, ClientLocationUpdateId = Guid.NewGuid().ToString(), RecordedAtUtc = stale ? Now.AddMinutes(-10) : Now, ReceivedAtUtc = Now, UpdatedAtUtc = Now, IsActive = true };
    private static EmergencyContact Contact(string id, string monitorId) => new() { Id = id, LinkedUserId = monitorId, IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Linked };
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => Now; }
    private sealed class Users(User user) : IUserRepository { public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult<User?>(user.Id == id ? user : null); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult<User?>(null); public Task AddAsync(User user, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(User user, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Incidents(params Incident[] items) : IIncidentRepository { public List<Incident> Items { get; } = items.ToList(); public Task<Incident?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id)); public Task<Incident?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult<Incident?>(null); public Task<(Incident Incident, bool IsDuplicate)> AddOrGetDuplicateAsync(Incident incident, CancellationToken ct) => Task.FromResult((incident, false)); public Task<IReadOnlyList<Incident>> ListByUserIdAsync(string u, IncidentStatus? s, string? t, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<Incident>>(Items.Where(i => i.UserId == u && (!s.HasValue || i.Status == s)).ToArray()); public Task<long> CountByUserIdAsync(string u, IncidentStatus? s, string? t, CancellationToken ct) => Task.FromResult((long)Items.Count(i => i.UserId == u && (!s.HasValue || i.Status == s))); public Task UpdateAsync(Incident incident, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Trips(params Trip[] items) : ITripRepository { public Task<Trip?> GetActiveByUserIdAsync(string u, CancellationToken ct) => Task.FromResult(items.FirstOrDefault(t => t.UserId == u && t.Status == TripStatus.Active)); public Task<Trip?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(items.FirstOrDefault(t => t.Id == id)); public Task<IReadOnlyList<Trip>> ListByUserIdAsync(string u, TripStatus? s, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<Trip>>([]); public Task<long> CountByUserIdAsync(string u, TripStatus? s, CancellationToken ct) => Task.FromResult(0L); public Task AddAsync(Trip trip, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(Trip trip, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Alerts(params AlertDispatchRequest[] items) : IAlertDispatchRepository { public Task<AlertDispatchRequest?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(items.FirstOrDefault(a => a.Id == id)); public Task<AlertDispatchRequest?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult<AlertDispatchRequest?>(null); public Task<(AlertDispatchRequest AlertDispatch, bool IsDuplicate)> AddOrGetDuplicateAsync(AlertDispatchRequest alert, CancellationToken ct) => Task.FromResult((alert, false)); public Task<IReadOnlyList<AlertDispatchRequest>> ListByIncidentIdAsync(string u, string i, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertDispatchRequest>>(items.Where(a => a.UserId == u && a.IncidentId == i).ToArray()); public Task<IReadOnlyList<AlertDispatchRequest>> ListByUserIdAsync(string u, AlertDispatchStatus? s, string? i, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertDispatchRequest>>(items.Where(a => a.UserId == u).ToArray()); public Task<long> CountByUserIdAsync(string u, AlertDispatchStatus? s, string? i, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(AlertDispatchRequest alert, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Attempts(params NotificationDeliveryAttempt[] items) : INotificationDeliveryAttemptRepository, INotificationAttemptMonitorRepository { public Task<NotificationDeliveryAttempt?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(items.FirstOrDefault(a => a.Id == id)); public Task<NotificationDeliveryAttempt?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult<NotificationDeliveryAttempt?>(null); public Task<(NotificationDeliveryAttempt Attempt, bool IsDuplicate)> AddOrGetDuplicateAsync(NotificationDeliveryAttempt attempt, CancellationToken ct) => Task.FromResult((attempt, false)); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByIncidentIdAsync(string u, string i, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>(items.Where(a => a.UserId == u && a.IncidentId == i).ToArray()); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByAlertDispatchIdAsync(string u, string a, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>(items.Where(n => n.UserId == u && n.AlertDispatchId == a).ToArray()); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByUserIdAsync(string u, string? a, string? i, NotificationDeliveryStatus? s, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>(items.Where(n => n.UserId == u).ToArray()); public Task<long> CountByUserIdAsync(string u, string? a, string? i, NotificationDeliveryStatus? s, CancellationToken ct) => Task.FromResult(0L); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByEmergencyContactIdsAsync(IReadOnlyCollection<string> ids, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>(items.Where(a => ids.Contains(a.EmergencyContactId)).ToArray()); public Task<long> CountByEmergencyContactIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(NotificationDeliveryAttempt attempt, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Acks(params AlertAcknowledgement[] items) : IAlertAcknowledgementRepository { public Task<AlertAcknowledgement?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(items.FirstOrDefault(a => a.Id == id)); public Task<AlertAcknowledgement?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult<AlertAcknowledgement?>(null); public Task<(AlertAcknowledgement Acknowledgement, bool IsDuplicate)> AddOrGetDuplicateAsync(AlertAcknowledgement ack, CancellationToken ct) => Task.FromResult((ack, false)); public Task<IReadOnlyList<AlertAcknowledgement>> ListByIncidentIdAsync(string u, string i, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertAcknowledgement>>(items.Where(a => a.UserId == u && a.IncidentId == i).ToArray()); public Task<IReadOnlyList<AlertAcknowledgement>> ListByAlertDispatchIdAsync(string u, string a, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertAcknowledgement>>(items.Where(n => n.UserId == u && n.AlertDispatchId == a).ToArray()); public Task<IReadOnlyList<AlertAcknowledgement>> ListByMonitorUserIdAsync(string m, AlertAcknowledgementStatus? s, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertAcknowledgement>>([]); public Task<long> CountByMonitorUserIdAsync(string m, AlertAcknowledgementStatus? s, CancellationToken ct) => Task.FromResult(0L); public Task<IReadOnlyList<AlertAcknowledgement>> ListByUserIdAsync(string u, string? a, string? i, AlertAcknowledgementStatus? s, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertAcknowledgement>>(items.Where(n => n.UserId == u).ToArray()); public Task<long> CountByUserIdAsync(string u, string? a, string? i, AlertAcknowledgementStatus? s, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(AlertAcknowledgement ack, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Locations(EmergencyLocationSnapshot? location = null) : ILocationSharingRepository { public Task<EmergencyLocationSnapshot?> GetByUserIdAndIncidentIdAsync(string u, string i, CancellationToken ct) => Task.FromResult<EmergencyLocationSnapshot?>(location); public Task<EmergencyLocationSnapshot?> GetActiveByIncidentIdAsync(string i, CancellationToken ct) => Task.FromResult(location is { IsActive: true } l && l.IncidentId == i ? l : null); public Task<EmergencyLocationSnapshot> UpsertLatestAsync(EmergencyLocationSnapshot snapshot, CancellationToken ct) => Task.FromResult(snapshot); }
    private sealed class Contacts(params EmergencyContact[] items) : IMonitorLinkedContactRepository { public Task<IReadOnlyList<EmergencyContact>> GetActiveLinkedByLinkedUserIdAsync(string id, CancellationToken ct) => Task.FromResult<IReadOnlyList<EmergencyContact>>(items.Where(c => c.LinkedUserId == id && c.IsActive && c.InvitationStatus == EmergencyContactInvitationStatus.Linked).ToArray()); }
}
