using FluentAssertions;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AlertAcknowledgements.Application;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
using MotoSOS.API.Modules.Incidents.Application;
using MotoSOS.API.Modules.Incidents.Domain;
using MotoSOS.API.Modules.LocationSharing.Application;
using MotoSOS.API.Modules.LocationSharing.Contracts;
using MotoSOS.API.Modules.LocationSharing.Domain;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.Onboarding.Application;
using MotoSOS.API.Modules.Onboarding.Contracts;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace UnitTest.LocationSharing;

public sealed class LocationSharingServiceTests
{
    [Fact]
    public async Task RiderCanShareLatestLocationOnlyForOwnOpenIncident()
    {
        User rider = User(UserRole.Rider);
        Incident incident = Incident(rider.Id);
        var locations = new Locations();
        LocationSharingService service = Service(rider, new Onboarding(), new Incidents(incident), locations, new Contacts(), new Attempts());

        ShareLocationSnapshotResponse created = await service.ShareAsync(rider.Id, Request(incident.Id, Now.AddMinutes(-1), latitude: 10), CancellationToken.None);
        ShareLocationSnapshotResponse ignoredOlder = await service.ShareAsync(rider.Id, Request(incident.Id, Now.AddMinutes(-2), latitude: 20), CancellationToken.None);

        created.Location.Latitude.Should().Be(10);
        ignoredOlder.Location.Latitude.Should().Be(10);
        locations.Current!.TripId.Should().Be(incident.TripId);
        locations.Current.MobileDeviceId.Should().Be(incident.MobileDeviceId);
        locations.Current.SmartwatchDeviceId.Should().Be(incident.SmartwatchDeviceId);
    }

    [Fact]
    public async Task ShareRejectsClosedAndFalsePositiveIncidents()
    {
        User rider = User(UserRole.Rider);
        LocationSharingService closedService = Service(rider, new Onboarding(), new Incidents(Incident(rider.Id, IncidentStatus.Closed)), new Locations(), new Contacts(), new Attempts());
        LocationSharingService falsePositiveService = Service(rider, new Onboarding(), new Incidents(Incident(rider.Id, IncidentStatus.FalsePositiveCancelled)), new Locations(), new Contacts(), new Attempts());

        await Assert.ThrowsAsync<IncidentNotReadyAppException>(() => closedService.ShareAsync(rider.Id, Request("incident"), CancellationToken.None));
        await Assert.ThrowsAsync<LocationSharingNotAllowedAppException>(() => falsePositiveService.ShareAsync(rider.Id, Request("incident"), CancellationToken.None));
    }

    [Fact]
    public async Task MonitorCanReadLocationOnlyForLinkedContactAttempt()
    {
        User monitor = User(UserRole.Monitor);
        Incident incident = Incident("rider");
        NotificationDeliveryAttempt attempt = Attempt(incident.Id, "contact-1");
        var locations = new Locations(Snapshot(incident));
        LocationSharingService service = Service(monitor, new Onboarding(), new Incidents(incident), locations, new Contacts(Contact("contact-1", monitor.Id)), new Attempts(attempt));

        GetLocationSnapshotResponse response = await service.GetForMonitorAsync(monitor.Id, attempt.Id, CancellationToken.None);

        response.Location.IncidentId.Should().Be(incident.Id);
        await Assert.ThrowsAsync<NotFoundAppException>(() => service.GetForMonitorAsync(monitor.Id, Attempt(incident.Id, "other-contact").Id, CancellationToken.None));
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 6, 14, 0, 0, TimeSpan.Zero);

    private static LocationSharingService Service(User user, IOnboardingService onboarding, IIncidentRepository incidents, ILocationSharingRepository locations, IMonitorLinkedContactRepository contacts, INotificationAttemptMonitorRepository attempts) => new(new Users(user), onboarding, incidents, locations, contacts, attempts, new LocationSharingStalenessService(), new Clock());
    private static User User(UserRole role) => new() { Email = $"{Guid.NewGuid()}@example.com", FullName = "User", Role = role, IsActive = true };
    private static Incident Incident(string userId, IncidentStatus status = IncidentStatus.Open) => new() { Id = "incident", UserId = userId, TripId = "trip", MobileDeviceId = "mobile", SmartwatchDeviceId = "watch", Status = status };
    private static EmergencyContact Contact(string id, string monitorId) => new() { Id = id, UserId = "rider", LinkedUserId = monitorId, IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Linked };
    private static NotificationDeliveryAttempt Attempt(string incidentId, string contactId) => new() { UserId = "rider", EmergencyContactId = contactId, AlertDispatchId = "alert", IncidentId = incidentId, TripId = "trip", Channel = NotificationChannel.Sms, Status = NotificationDeliveryStatus.Prepared, Provider = NotificationProvider.None, PreparedAtUtc = Now, CreatedAtUtc = Now };
    private static ShareLocationSnapshotRequest Request(string incidentId, DateTimeOffset? recordedAtUtc = null, double latitude = 10) => new(incidentId, Guid.NewGuid().ToString(), latitude, -99, 5, null, null, null, 80, "MobileApp", recordedAtUtc ?? Now);
    private static EmergencyLocationSnapshot Snapshot(Incident incident) => new() { UserId = incident.UserId, IncidentId = incident.Id, TripId = incident.TripId, Latitude = 10, Longitude = -99, Source = LocationSharingSource.MobileApp, ClientLocationUpdateId = Guid.NewGuid().ToString(), RecordedAtUtc = Now, ReceivedAtUtc = Now, UpdatedAtUtc = Now, IsActive = true };

    private sealed class Clock : IClock { public DateTimeOffset UtcNow => Now; }
    private sealed class Users(User user) : IUserRepository { public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult<User?>(user.Id == id ? user : null); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult<User?>(null); public Task AddAsync(User u, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(User u, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Onboarding : IOnboardingService { public Task<OnboardingStatusResponse> GetStatusAsync(string userId, CancellationToken ct) => Task.FromResult(new OnboardingStatusResponse(7, 7, 100, "Completed", true, [])); }
    private sealed class Incidents(params Incident[] incidents) : IIncidentRepository { private readonly List<Incident> _items = incidents.ToList(); public Task<Incident?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(_items.FirstOrDefault(i => i.Id == id)); public Task<Incident?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) => Task.FromResult<Incident?>(null); public Task<(Incident Incident, bool IsDuplicate)> AddOrGetDuplicateAsync(Incident incident, CancellationToken ct) => Task.FromResult((incident, false)); public Task<IReadOnlyList<Incident>> ListByUserIdAsync(string userId, IncidentStatus? status, string? tripId, int pageNumber, int pageSize, CancellationToken ct) => Task.FromResult<IReadOnlyList<Incident>>(_items.Where(i => i.UserId == userId).ToArray()); public Task<long> CountByUserIdAsync(string userId, IncidentStatus? status, string? tripId, CancellationToken ct) => Task.FromResult((long)_items.Count(i => i.UserId == userId)); public Task UpdateAsync(Incident incident, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Locations(EmergencyLocationSnapshot? current = null) : ILocationSharingRepository { public EmergencyLocationSnapshot? Current { get; private set; } = current; public Task<EmergencyLocationSnapshot?> GetByUserIdAndIncidentIdAsync(string userId, string incidentId, CancellationToken ct) => Task.FromResult(Current is { } s && s.UserId == userId && s.IncidentId == incidentId ? s : null); public Task<EmergencyLocationSnapshot?> GetActiveByIncidentIdAsync(string incidentId, CancellationToken ct) => Task.FromResult(Current is { IsActive: true } s && s.IncidentId == incidentId ? s : null); public Task<EmergencyLocationSnapshot> UpsertLatestAsync(EmergencyLocationSnapshot snapshot, CancellationToken ct) { Current = snapshot; return Task.FromResult(snapshot); } }
    private sealed class Contacts(params EmergencyContact[] contacts) : IMonitorLinkedContactRepository { public Task<IReadOnlyList<EmergencyContact>> GetActiveLinkedByLinkedUserIdAsync(string id, CancellationToken ct) => Task.FromResult<IReadOnlyList<EmergencyContact>>(contacts.Where(c => c.LinkedUserId == id && c.IsActive && c.InvitationStatus == EmergencyContactInvitationStatus.Linked).ToArray()); }
    private sealed class Attempts(params NotificationDeliveryAttempt[] attempts) : INotificationAttemptMonitorRepository { private readonly List<NotificationDeliveryAttempt> _items = attempts.ToList(); public Task<NotificationDeliveryAttempt?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(_items.FirstOrDefault(a => a.Id == id)); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByEmergencyContactIdsAsync(IReadOnlyCollection<string> ids, int pageNumber, int pageSize, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>(_items.Where(a => ids.Contains(a.EmergencyContactId)).ToArray()); public Task<long> CountByEmergencyContactIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct) => Task.FromResult((long)_items.Count(a => ids.Contains(a.EmergencyContactId))); }
}
