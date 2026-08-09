using FluentAssertions;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AlertAcknowledgements.Application;
using MotoSOS.API.Modules.AlertAcknowledgements.Domain;
using MotoSOS.API.Modules.AlertDispatch.Application;
using MotoSOS.API.Modules.AlertDispatch.Domain;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
using MotoSOS.API.Modules.EmergencyResolution.Application;
using MotoSOS.API.Modules.EmergencyResolution.Contracts;
using MotoSOS.API.Modules.EmergencyResolution.Domain;
using MotoSOS.API.Modules.Incidents.Application;
using MotoSOS.API.Modules.Incidents.Domain;
using MotoSOS.API.Modules.LocationSharing.Application;
using MotoSOS.API.Modules.LocationSharing.Domain;
using MotoSOS.API.Modules.Notifications.Application;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace UnitTest.EmergencyResolution;

public sealed class EmergencyResolutionServiceTests
{
    [Fact]
    public async Task RiderCanCreateClosedReportWithMetricsAndLatestLocation()
    {
        User rider = User(UserRole.Rider); Incident incident = Incident(rider.Id, IncidentStatus.Closed); AlertDispatchRequest dispatch = Dispatch(rider.Id, incident.Id); var locations = new Locations(Snapshot(incident, active: false));
        var reports = new Reports();
        var service = Service(rider, new Incidents(incident), alerts: new Alerts(dispatch), attempts: new Attempts(Attempt(rider.Id, incident.Id, dispatch.Id, Now.AddMinutes(2))), acks: new Acks(Ack(rider.Id, incident.Id, dispatch.Id, AlertAcknowledgementStatus.Acknowledged, Now.AddMinutes(5)), Ack(rider.Id, incident.Id, dispatch.Id, AlertAcknowledgementStatus.Declined, null)), locations: locations, reports: reports);

        CreateEmergencyResolutionReportResponse response = await service.CreateForRiderAsync(rider.Id, incident.Id, Request("RealEmergency"), CancellationToken.None);
        CreateEmergencyResolutionReportResponse duplicate = await service.CreateForRiderAsync(rider.Id, incident.Id, Request("FalsePositive"), CancellationToken.None);

        response.IsDuplicate.Should().BeFalse();
        duplicate.IsDuplicate.Should().BeTrue();
        reports.Items.Should().HaveCount(1);
        response.Report.NotificationAttemptsTotal.Should().Be(1);
        response.Report.AcknowledgementsTotal.Should().Be(2);
        response.Report.AcknowledgedCount.Should().Be(1);
        response.Report.DeclinedCount.Should().Be(1);
        response.Report.ResponseTimeSeconds.Should().Be(300);
        response.Report.FinalLatitude.Should().Be(19);
        response.Report.LastKnownLocationWasStale.Should().BeTrue();
    }

    [Fact]
    public async Task FalsePositiveCancelledCanCreateReportButOpenAndForeignCannot()
    {
        User rider = User(UserRole.Rider); Incident cancelled = Incident(rider.Id, IncidentStatus.FalsePositiveCancelled); Incident open = Incident(rider.Id, IncidentStatus.Open); Incident foreign = Incident("owner", IncidentStatus.Closed);
        await Service(rider, new Incidents(cancelled)).CreateForRiderAsync(rider.Id, cancelled.Id, Request("FalsePositive"), CancellationToken.None);
        await Assert.ThrowsAsync<IncidentNotReadyAppException>(() => Service(rider, new Incidents(open)).CreateForRiderAsync(rider.Id, open.Id, Request("RealEmergency"), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundAppException>(() => Service(rider, new Incidents(foreign)).CreateForRiderAsync(rider.Id, foreign.Id, Request("RealEmergency"), CancellationToken.None));
    }

    [Fact]
    public async Task AdminAndMonitorCannotCreateReports()
    {
        Incident incident = Incident("user", IncidentStatus.Closed);
        await Assert.ThrowsAsync<ForbiddenAppException>(() => Service(User(UserRole.Admin), new Incidents(incident)).CreateForRiderAsync("admin", incident.Id, Request("RealEmergency"), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenAppException>(() => Service(User(UserRole.Monitor), new Incidents(incident)).CreateForRiderAsync("monitor", incident.Id, Request("RealEmergency"), CancellationToken.None));
    }

    [Fact]
    public async Task RiderAndAssignedMonitorCanReadOnlyAllowedReports()
    {
        User rider = User(UserRole.Rider); User monitor = User(UserRole.Monitor); Incident incident = Incident(rider.Id, IncidentStatus.Closed); var report = Report(rider.Id, incident.Id); NotificationDeliveryAttempt attempt = Attempt(rider.Id, incident.Id, "alert", Now, "contact-1");
        (await Service(rider, new Incidents(incident), reports: new Reports(report)).GetForRiderAsync(rider.Id, incident.Id, CancellationToken.None)).Report.Id.Should().Be(report.Id);
        (await Service(monitor, new Incidents(incident), attempts: new Attempts(attempt), contacts: new Contacts(Contact("contact-1", monitor.Id)), reports: new Reports(report)).GetForMonitorAsync(monitor.Id, attempt.Id, CancellationToken.None)).Report.Id.Should().Be(report.Id);
        await Assert.ThrowsAsync<NotFoundAppException>(() => Service(monitor, new Incidents(incident), attempts: new Attempts(attempt), contacts: new Contacts(Contact("other", monitor.Id)), reports: new Reports(report)).GetForMonitorAsync(monitor.Id, attempt.Id, CancellationToken.None));
    }

    [Fact]
    public async Task ListForRiderReturnsOnlyOwnReports()
    {
        User rider = User(UserRole.Rider); var reports = new Reports(Report(rider.Id, "incident-1"), Report("other", "incident-2"));

        GetEmergencyResolutionReportsResponse response = await Service(rider, reports: reports).ListForRiderAsync(rider.Id, null, 1, 20, CancellationToken.None);

        response.Reports.Should().ContainSingle(r => r.UserId == rider.Id);
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);
    private static EmergencyResolutionService Service(User user, Incidents? incidents = null, Alerts? alerts = null, Attempts? attempts = null, Acks? acks = null, Locations? locations = null, Contacts? contacts = null, Reports? reports = null) => new(new Users(user), incidents ?? new Incidents(), alerts ?? new Alerts(), attempts ?? new Attempts(), acks ?? new Acks(), locations ?? new Locations(), contacts ?? new Contacts(), attempts ?? new Attempts(), reports ?? new Reports(), new EmergencyResolutionIdempotencyKeyFactory(), new LocationSharingStalenessService(), new Clock());
    private static CreateEmergencyResolutionReportRequest Request(string outcome) => new(outcome, "Resolved safely", "No sensitive data");
    private static User User(UserRole role) => new() { Id = role.ToString().ToLowerInvariant(), Email = $"{Guid.NewGuid()}@example.com", FullName = "User", Role = role, IsActive = true };
    private static Incident Incident(string userId, IncidentStatus status) => new() { Id = $"incident-{Guid.NewGuid():N}", UserId = userId, TripId = "trip", Status = status, Source = IncidentSource.MobileDetection, Cause = IncidentCause.CountdownTimeout, RiskLevel = IncidentRiskLevel.High, CreatedAtUtc = Now, OccurredAtUtc = Now, ClosedAtUtc = status == IncidentStatus.Closed ? Now.AddMinutes(10) : null, CancelledAtUtc = status == IncidentStatus.FalsePositiveCancelled ? Now.AddMinutes(3) : null, ClosedByUserId = userId };
    private static AlertDispatchRequest Dispatch(string userId, string incidentId) => new() { Id = "alert", UserId = userId, IncidentId = incidentId, TripId = "trip", CreatedAtUtc = Now, RequestedAtUtc = Now };
    private static NotificationDeliveryAttempt Attempt(string userId, string incidentId, string alertId, DateTimeOffset preparedAt, string contactId = "contact") => new() { Id = $"attempt-{Guid.NewGuid():N}", UserId = userId, IncidentId = incidentId, AlertDispatchId = alertId, TripId = "trip", EmergencyContactId = contactId, PreparedAtUtc = preparedAt, CreatedAtUtc = preparedAt };
    private static AlertAcknowledgement Ack(string userId, string incidentId, string alertId, AlertAcknowledgementStatus status, DateTimeOffset? acknowledgedAt) => new() { UserId = userId, IncidentId = incidentId, AlertDispatchId = alertId, TripId = "trip", EmergencyContactId = "contact", NotificationDeliveryAttemptId = "attempt", Status = status, AcknowledgedAtUtc = acknowledgedAt, DeclinedAtUtc = status == AlertAcknowledgementStatus.Declined ? Now.AddMinutes(6) : null, CreatedAtUtc = Now };
    private static EmergencyLocationSnapshot Snapshot(Incident incident, bool active) => new() { UserId = incident.UserId, IncidentId = incident.Id, TripId = incident.TripId, Latitude = 19, Longitude = -99, Source = LocationSharingSource.MobileApp, ClientLocationUpdateId = "location", RecordedAtUtc = Now.AddMinutes(-10), ReceivedAtUtc = Now.AddMinutes(-9), IsActive = active };
    private static EmergencyContact Contact(string id, string monitorId) => new() { Id = id, LinkedUserId = monitorId, IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Linked };
    private static EmergencyResolutionReport Report(string userId, string incidentId) => new() { Id = $"report-{Guid.NewGuid():N}", UserId = userId, IncidentId = incidentId, TripId = "trip", IdempotencyKey = $"key-{incidentId}", Outcome = EmergencyResolutionOutcome.RealEmergency, Summary = "Resolved", IncidentCreatedAtUtc = Now, IncidentClosedAtUtc = Now.AddMinutes(10), CreatedAtUtc = Now };
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => Now; }
    private sealed class Users(User user) : IUserRepository { public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult<User?>(user.Id == id ? user : null); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult<User?>(null); public Task AddAsync(User user, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(User user, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Incidents(params Incident[] items) : IIncidentRepository { public Task<Incident?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(items.FirstOrDefault(i => i.Id == id)); public Task<Incident?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult<Incident?>(null); public Task<(Incident Incident, bool IsDuplicate)> AddOrGetDuplicateAsync(Incident incident, CancellationToken ct) => Task.FromResult((incident, false)); public Task<IReadOnlyList<Incident>> ListByUserIdAsync(string u, IncidentStatus? s, string? t, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<Incident>>([]); public Task<long> CountByUserIdAsync(string u, IncidentStatus? s, string? t, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(Incident incident, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Alerts(params AlertDispatchRequest[] items) : IAlertDispatchRepository { public Task<AlertDispatchRequest?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(items.FirstOrDefault(a => a.Id == id)); public Task<AlertDispatchRequest?> GetByIdempotencyKeyAsync(string k, CancellationToken ct) => Task.FromResult<AlertDispatchRequest?>(null); public Task<(AlertDispatchRequest AlertDispatch, bool IsDuplicate)> AddOrGetDuplicateAsync(AlertDispatchRequest a, CancellationToken ct) => Task.FromResult((a, false)); public Task<IReadOnlyList<AlertDispatchRequest>> ListByIncidentIdAsync(string u, string i, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertDispatchRequest>>(items.Where(a => a.UserId == u && a.IncidentId == i).ToArray()); public Task<IReadOnlyList<AlertDispatchRequest>> ListByUserIdAsync(string u, AlertDispatchStatus? s, string? i, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertDispatchRequest>>([]); public Task<long> CountByUserIdAsync(string u, AlertDispatchStatus? s, string? i, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(AlertDispatchRequest a, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Attempts(params NotificationDeliveryAttempt[] items) : INotificationDeliveryAttemptRepository, INotificationAttemptMonitorRepository { public Task<NotificationDeliveryAttempt?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(items.FirstOrDefault(a => a.Id == id)); public Task<NotificationDeliveryAttempt?> GetByIdempotencyKeyAsync(string k, CancellationToken ct) => Task.FromResult<NotificationDeliveryAttempt?>(null); public Task<(NotificationDeliveryAttempt Attempt, bool IsDuplicate)> AddOrGetDuplicateAsync(NotificationDeliveryAttempt a, CancellationToken ct) => Task.FromResult((a, false)); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByIncidentIdAsync(string u, string i, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>(items.Where(a => a.UserId == u && a.IncidentId == i).ToArray()); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByAlertDispatchIdAsync(string u, string a, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>(items.Where(n => n.UserId == u && n.AlertDispatchId == a).ToArray()); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByUserIdAsync(string u, string? a, string? i, NotificationDeliveryStatus? s, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>([]); public Task<long> CountByUserIdAsync(string u, string? a, string? i, NotificationDeliveryStatus? s, CancellationToken ct) => Task.FromResult(0L); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByEmergencyContactIdsAsync(IReadOnlyCollection<string> ids, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>(items.Where(a => ids.Contains(a.EmergencyContactId)).ToArray()); public Task<long> CountByEmergencyContactIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(NotificationDeliveryAttempt a, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Acks(params AlertAcknowledgement[] items) : IAlertAcknowledgementRepository { public Task<AlertAcknowledgement?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(items.FirstOrDefault(a => a.Id == id)); public Task<AlertAcknowledgement?> GetByIdempotencyKeyAsync(string k, CancellationToken ct) => Task.FromResult<AlertAcknowledgement?>(null); public Task<(AlertAcknowledgement Acknowledgement, bool IsDuplicate)> AddOrGetDuplicateAsync(AlertAcknowledgement a, CancellationToken ct) => Task.FromResult((a, false)); public Task<IReadOnlyList<AlertAcknowledgement>> ListByIncidentIdAsync(string u, string i, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertAcknowledgement>>(items.Where(a => a.UserId == u && a.IncidentId == i).ToArray()); public Task<IReadOnlyList<AlertAcknowledgement>> ListByAlertDispatchIdAsync(string u, string a, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertAcknowledgement>>(items.Where(n => n.UserId == u && n.AlertDispatchId == a).ToArray()); public Task<IReadOnlyList<AlertAcknowledgement>> ListByMonitorUserIdAsync(string m, AlertAcknowledgementStatus? s, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertAcknowledgement>>([]); public Task<long> CountByMonitorUserIdAsync(string m, AlertAcknowledgementStatus? s, CancellationToken ct) => Task.FromResult(0L); public Task<IReadOnlyList<AlertAcknowledgement>> ListByUserIdAsync(string u, string? a, string? i, AlertAcknowledgementStatus? s, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertAcknowledgement>>([]); public Task<long> CountByUserIdAsync(string u, string? a, string? i, AlertAcknowledgementStatus? s, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(AlertAcknowledgement a, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Locations(EmergencyLocationSnapshot? location = null) : ILocationSharingRepository { public Task<EmergencyLocationSnapshot?> GetByUserIdAndIncidentIdAsync(string u, string i, CancellationToken ct) => Task.FromResult(location); public Task<EmergencyLocationSnapshot?> GetActiveByIncidentIdAsync(string i, CancellationToken ct) => Task.FromResult(location is { IsActive: true } l && l.IncidentId == i ? l : null); public Task<EmergencyLocationSnapshot?> GetLatestByIncidentIdAsync(string i, CancellationToken ct) => Task.FromResult(location is { } l && l.IncidentId == i ? l : null); public Task<EmergencyLocationSnapshot> UpsertLatestAsync(EmergencyLocationSnapshot s, CancellationToken ct) => Task.FromResult(s); }
    private sealed class Contacts(params EmergencyContact[] items) : IMonitorLinkedContactRepository { public Task<IReadOnlyList<EmergencyContact>> GetActiveLinkedByLinkedUserIdAsync(string id, CancellationToken ct) => Task.FromResult<IReadOnlyList<EmergencyContact>>(items.Where(c => c.LinkedUserId == id && c.IsActive && c.InvitationStatus == EmergencyContactInvitationStatus.Linked).ToArray()); }
    private sealed class Reports(params EmergencyResolutionReport[] reports) : IEmergencyResolutionRepository { public List<EmergencyResolutionReport> Items { get; } = reports.ToList(); public Task<EmergencyResolutionReport?> GetByIncidentIdAsync(string i, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(r => r.IncidentId == i)); public Task<EmergencyResolutionReport?> GetByIdempotencyKeyAsync(string k, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(r => r.IdempotencyKey == k)); public Task<(EmergencyResolutionReport Report, bool IsDuplicate)> AddOrGetDuplicateAsync(EmergencyResolutionReport r, CancellationToken ct) { EmergencyResolutionReport? existing = Items.FirstOrDefault(x => x.IdempotencyKey == r.IdempotencyKey || x.IncidentId == r.IncidentId); if (existing is not null) return Task.FromResult((existing, true)); Items.Add(r); return Task.FromResult((r, false)); } public Task<IReadOnlyList<EmergencyResolutionReport>> ListByUserIdAsync(string u, EmergencyResolutionOutcome? o, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<EmergencyResolutionReport>>(Items.Where(r => r.UserId == u && (!o.HasValue || r.Outcome == o)).Skip((p - 1) * z).Take(z).ToArray()); public Task<long> CountByUserIdAsync(string u, EmergencyResolutionOutcome? o, CancellationToken ct) => Task.FromResult((long)Items.Count(r => r.UserId == u && (!o.HasValue || r.Outcome == o))); }
}
