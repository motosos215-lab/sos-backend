using FluentAssertions;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AlertDispatch.Application;
using MotoSOS.API.Modules.AlertDispatch.Contracts;
using MotoSOS.API.Modules.AlertDispatch.Domain;
using MotoSOS.API.Modules.EmergencyContacts.Application;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
using MotoSOS.API.Modules.Incidents.Application;
using MotoSOS.API.Modules.Incidents.Domain;
using MotoSOS.API.Modules.Onboarding.Application;
using MotoSOS.API.Modules.Onboarding.Contracts;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace UnitTest.AlertDispatch;

public sealed class AlertDispatchServiceTests
{
    [Fact]
    public async Task RiderCanCreateWithReadyOnboardingOpenOwnIncidentAndEligibleContacts()
    {
        User user = User(UserRole.Rider); Incident incident = Incident(user.Id, IncidentStatus.Open); var alerts = new Alerts();
        CreateAlertDispatchResponse response = await Service(user, alerts: alerts, incidents: new Incidents(incident), contacts: new Contacts(Contact(user.Id))).CreateAsync(user.Id, Request(incident.Id), CancellationToken.None);

        response.AlertDispatch.Status.Should().Be("PendingDispatch");
        response.AlertDispatch.TripId.Should().Be(incident.TripId);
        response.AlertDispatch.VehicleId.Should().Be(incident.VehicleId);
        response.AlertDispatch.MobileDeviceId.Should().Be(incident.MobileDeviceId);
        response.AlertDispatch.ContactsCount.Should().Be(1);
        alerts.Items.Single().ContactsSnapshot.Should().ContainSingle(c => c.InvitationStatus == EmergencyContactInvitationStatus.Invited);
    }

    [Fact]
    public async Task CreateRejectsIncompleteOnboardingForeignClosedFalsePositiveAndMissingContacts()
    {
        User user = User(UserRole.Rider); User other = User(UserRole.Rider);
        await Assert.ThrowsAsync<OnboardingNotReadyAppException>(() => Service(user, ready: false).CreateAsync(user.Id, Request("incident"), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundAppException>(() => Service(user, incidents: new Incidents(Incident(other.Id, IncidentStatus.Open)), contacts: new Contacts(Contact(user.Id))).CreateAsync(user.Id, Request("incident"), CancellationToken.None));
        Incident closed = Incident(user.Id, IncidentStatus.Closed); await Assert.ThrowsAsync<IncidentNotReadyAppException>(() => Service(user, incidents: new Incidents(closed), contacts: new Contacts(Contact(user.Id))).CreateAsync(user.Id, Request(closed.Id), CancellationToken.None));
        Incident cancelled = Incident(user.Id, IncidentStatus.FalsePositiveCancelled); await Assert.ThrowsAsync<AlertNotAllowedAppException>(() => Service(user, incidents: new Incidents(cancelled), contacts: new Contacts(Contact(user.Id))).CreateAsync(user.Id, Request(cancelled.Id), CancellationToken.None));
        Incident open = Incident(user.Id, IncidentStatus.Open); await Assert.ThrowsAsync<AlertNotAllowedAppException>(() => Service(user, incidents: new Incidents(open), contacts: new Contacts()).CreateAsync(user.Id, Request(open.Id), CancellationToken.None));
    }

    [Fact]
    public async Task DuplicateReturnsExistingAndDoesNotCreateDuplicate()
    {
        User user = User(UserRole.Rider); Incident incident = Incident(user.Id, IncidentStatus.Open); var alerts = new Alerts(); AlertDispatchService service = Service(user, alerts: alerts, incidents: new Incidents(incident), contacts: new Contacts(Contact(user.Id))); string clientId = Guid.NewGuid().ToString();

        CreateAlertDispatchResponse first = await service.CreateAsync(user.Id, Request(incident.Id, clientId), CancellationToken.None);
        CreateAlertDispatchResponse duplicate = await service.CreateAsync(user.Id, Request(incident.Id, clientId), CancellationToken.None);

        duplicate.AlertDispatch.Id.Should().Be(first.AlertDispatch.Id);
        alerts.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task ListGetAndCancelRespectOwnershipAndStateTransitions()
    {
        User user = User(UserRole.Rider); User other = User(UserRole.Rider); AlertDispatchRequest own = Alert(user.Id, AlertDispatchStatus.PendingDispatch); AlertDispatchRequest otherAlert = Alert(other.Id, AlertDispatchStatus.PendingDispatch); var alerts = new Alerts(own, otherAlert); AlertDispatchService service = Service(user, alerts: alerts);

        (await service.ListAsync(user.Id, null, null, null, null, CancellationToken.None)).AlertDispatches.Should().ContainSingle(a => a.Id == own.Id);
        await Assert.ThrowsAsync<NotFoundAppException>(() => service.GetAsync(user.Id, otherAlert.Id, CancellationToken.None));
        CancelAlertDispatchResponse cancelled = await service.CancelAsync(user.Id, own.Id, new CancelAlertDispatchRequest("cancel", Now), CancellationToken.None);
        CancelAlertDispatchResponse secondCancel = await service.CancelAsync(user.Id, own.Id, new CancelAlertDispatchRequest("cancel", Now), CancellationToken.None);

        cancelled.AlertDispatch.Status.Should().Be("Cancelled");
        secondCancel.AlertDispatch.Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task CompletedCancelFailsAndNonRidersAreForbidden()
    {
        User rider = User(UserRole.Rider); AlertDispatchRequest completed = Alert(rider.Id, AlertDispatchStatus.Completed); await Assert.ThrowsAsync<AlertDispatchAlreadyCompletedAppException>(() => Service(rider, alerts: new Alerts(completed)).CancelAsync(rider.Id, completed.Id, new CancelAlertDispatchRequest(null, null), CancellationToken.None));
        User monitor = User(UserRole.Monitor); await Assert.ThrowsAsync<ForbiddenAppException>(() => Service(monitor).ListAsync(monitor.Id, null, null, null, null, CancellationToken.None));
        User admin = User(UserRole.Admin); await Assert.ThrowsAsync<ForbiddenAppException>(() => Service(admin).ListAsync(admin.Id, null, null, null, null, CancellationToken.None));
    }

    [Fact]
    public void ServiceDoesNotHaveNotificationOrEscalationDependencies()
    {
        typeof(AlertDispatchService).GetConstructors().Single().GetParameters().Select(p => p.ParameterType.Name).Should().NotContain(name => name.Contains("Push", StringComparison.OrdinalIgnoreCase) || name.Contains("Sms", StringComparison.OrdinalIgnoreCase) || name.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase) || name.Contains("Email", StringComparison.OrdinalIgnoreCase) || name.Contains("Escalation", StringComparison.OrdinalIgnoreCase));
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 6, 7, 12, 0, TimeSpan.Zero);
    private static AlertDispatchService Service(User user, bool ready = true, Alerts? alerts = null, Incidents? incidents = null, Contacts? contacts = null) => new(new Users(user), new StubOnboarding(ready), incidents ?? new Incidents(), contacts ?? new Contacts(), alerts ?? new Alerts(), new AlertDispatchIdempotencyKeyFactory(), new Clock());
    private static User User(UserRole role) => new() { Email = $"{Guid.NewGuid()}@example.com", FullName = "Rider", Role = role, IsActive = true };
    private static Incident Incident(string userId, IncidentStatus status) => new() { UserId = userId, TripId = "trip", VehicleId = "vehicle", MobileDeviceId = "mobile", SmartwatchDeviceId = "watch", ClientIncidentId = Guid.NewGuid().ToString(), IdempotencyKey = Guid.NewGuid().ToString(), Source = IncidentSource.MobileDetection, Cause = IncidentCause.CountdownTimeout, RiskLevel = IncidentRiskLevel.High, Status = status, OccurredAtUtc = Now, CreatedAtUtc = Now };
    private static AlertDispatchRequest Alert(string userId, AlertDispatchStatus status) => new() { UserId = userId, IncidentId = "incident", TripId = "trip", VehicleId = "vehicle", MobileDeviceId = "mobile", ClientAlertRequestId = Guid.NewGuid().ToString(), IdempotencyKey = Guid.NewGuid().ToString(), Priority = AlertDispatchPriority.High, Reason = AlertDispatchReason.IncidentCreated, Status = status, RequestedAtUtc = Now, CreatedAtUtc = Now, UpdatedAtUtc = Now, ContactsSnapshot = [new AlertContactSnapshot { EmergencyContactId = "contact", InvitationStatus = EmergencyContactInvitationStatus.Invited }] };
    private static EmergencyContact Contact(string userId) => new() { UserId = userId, FullName = "Contact", IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Invited };
    private static CreateAlertDispatchRequest Request(string incidentId, string? clientId = null) => new(incidentId, clientId ?? Guid.NewGuid().ToString(), "High", "IncidentCreated", Now, "prepared");
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => Now; }
    private sealed class StubOnboarding(bool ready) : IOnboardingService { public Task<OnboardingStatusResponse> GetStatusAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(ready ? new OnboardingStatusResponse(7, 7, 100, "Completed", true, []) : new OnboardingStatusResponse(7, 6, 86, "Confirmation", false, [])); }
    private sealed class Users(User user) : IUserRepository { public Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult<User?>(user.Id == id ? user : null); public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) => Task.FromResult<User?>(null); public Task AddAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask; public Task UpdateAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class Incidents(params Incident[] incidents) : IIncidentRepository { private readonly List<Incident> _items = incidents.ToList(); public Task<Incident?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(_items.FirstOrDefault(i => i.Id == id)); public Task<Incident?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult(_items.FirstOrDefault(i => i.IdempotencyKey == key)); public Task<(Incident Incident, bool IsDuplicate)> AddOrGetDuplicateAsync(Incident incident, CancellationToken ct) => Task.FromResult((incident, false)); public Task<IReadOnlyList<Incident>> ListByUserIdAsync(string u, IncidentStatus? s, string? t, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<Incident>>([]); public Task<long> CountByUserIdAsync(string u, IncidentStatus? s, string? t, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(Incident incident, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Contacts(params EmergencyContact[] contacts) : IEmergencyContactRepository { public Task<IReadOnlyList<EmergencyContact>> GetActiveByUserIdAsync(string u, CancellationToken ct) => Task.FromResult<IReadOnlyList<EmergencyContact>>(contacts.Where(c => c.UserId == u && c.IsActive).ToArray()); public Task<EmergencyContact?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult<EmergencyContact?>(null); public Task<EmergencyContact?> GetByLinkingCodeAsync(string code, CancellationToken ct) => Task.FromResult<EmergencyContact?>(null); public Task<int> CountActiveByUserIdAsync(string u, CancellationToken ct) => Task.FromResult(0); public Task AddAsync(EmergencyContact contact, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(EmergencyContact contact, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Alerts(params AlertDispatchRequest[] alerts) : IAlertDispatchRepository { public List<AlertDispatchRequest> Items { get; } = alerts.ToList(); public Task<AlertDispatchRequest?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.Id == id)); public Task<AlertDispatchRequest?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.IdempotencyKey == key)); public Task<(AlertDispatchRequest AlertDispatch, bool IsDuplicate)> AddOrGetDuplicateAsync(AlertDispatchRequest alert, CancellationToken ct) { AlertDispatchRequest? existing = Items.FirstOrDefault(a => a.IdempotencyKey == alert.IdempotencyKey); if (existing is not null) return Task.FromResult((existing, true)); Items.Add(alert); return Task.FromResult((alert, false)); } public Task<IReadOnlyList<AlertDispatchRequest>> ListByUserIdAsync(string u, AlertDispatchStatus? s, string? i, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertDispatchRequest>>(Items.Where(a => a.UserId == u && (!s.HasValue || a.Status == s) && (i is null || a.IncidentId == i)).ToArray()); public Task<long> CountByUserIdAsync(string u, AlertDispatchStatus? s, string? i, CancellationToken ct) => Task.FromResult((long)Items.Count(a => a.UserId == u)); public Task UpdateAsync(AlertDispatchRequest alert, CancellationToken ct) => Task.CompletedTask; }
}
