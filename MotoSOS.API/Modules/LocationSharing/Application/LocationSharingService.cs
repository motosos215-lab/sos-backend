using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AlertAcknowledgements.Application;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
using MotoSOS.API.Modules.Incidents.Application;
using MotoSOS.API.Modules.Incidents.Domain;
using MotoSOS.API.Modules.LocationSharing.Contracts;
using MotoSOS.API.Modules.LocationSharing.Domain;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.Onboarding.Application;
using MotoSOS.API.Modules.Onboarding.Contracts;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Modules.LocationSharing.Application;

public sealed class LocationSharingService : ILocationSharingService
{
    private readonly IUserRepository _users;
    private readonly IOnboardingService _onboarding;
    private readonly IIncidentRepository _incidents;
    private readonly ILocationSharingRepository _locations;
    private readonly IMonitorLinkedContactRepository _contacts;
    private readonly INotificationAttemptMonitorRepository _attempts;
    private readonly ILocationSharingStalenessService _staleness;
    private readonly IClock _clock;
    public LocationSharingService(IUserRepository users, IOnboardingService onboarding, IIncidentRepository incidents, ILocationSharingRepository locations, IMonitorLinkedContactRepository contacts, INotificationAttemptMonitorRepository attempts, ILocationSharingStalenessService staleness, IClock clock)
    {
        _users = users; _onboarding = onboarding; _incidents = incidents; _locations = locations; _contacts = contacts; _attempts = attempts; _staleness = staleness; _clock = clock;
    }
    public async Task<ShareLocationSnapshotResponse> ShareAsync(string userId, ShareLocationSnapshotRequest request, CancellationToken cancellationToken)
    {
        User rider = await GetUserAsync(userId, UserRole.Rider, cancellationToken);
        await EnsureOnboardingReadyAsync(rider.Id, cancellationToken);
        DateTimeOffset now = _clock.UtcNow;
        if (request.RecordedAtUtc!.Value > now.AddMinutes(2)) throw new ValidationAppException("RecordedAtUtc cannot be more than 2 minutes in the future.");
        Incident incident = await GetOwnedIncidentAsync(rider.Id, request.IncidentId!, cancellationToken);
        if (incident.Status == IncidentStatus.Closed) throw new IncidentNotReadyAppException("Closed incidents cannot share location.");
        if (incident.Status == IncidentStatus.FalsePositiveCancelled) throw new LocationSharingNotAllowedAppException("False positive incidents cannot share location.");
        EmergencyLocationSnapshot? current = await _locations.GetByUserIdAndIncidentIdAsync(rider.Id, incident.Id, cancellationToken);
        if (current is not null && (current.ClientLocationUpdateId == request.ClientLocationUpdateId || current.RecordedAtUtc >= request.RecordedAtUtc.Value)) return new ShareLocationSnapshotResponse(ToResponse(current, now));
        var snapshot = new EmergencyLocationSnapshot { Id = current?.Id ?? MongoDB.Bson.ObjectId.GenerateNewId().ToString(), UserId = rider.Id, IncidentId = incident.Id, TripId = incident.TripId, MobileDeviceId = incident.MobileDeviceId, SmartwatchDeviceId = incident.SmartwatchDeviceId, Latitude = request.Latitude!.Value, Longitude = request.Longitude!.Value, AccuracyMeters = request.AccuracyMeters, AltitudeMeters = request.AltitudeMeters, SpeedMetersPerSecond = request.SpeedMetersPerSecond, HeadingDegrees = request.HeadingDegrees, BatteryPercentage = request.BatteryPercentage, Source = Parse<LocationSharingSource>(request.Source)!.Value, ClientLocationUpdateId = request.ClientLocationUpdateId!.Trim(), RecordedAtUtc = request.RecordedAtUtc.Value, ReceivedAtUtc = current?.ReceivedAtUtc ?? now, UpdatedAtUtc = now, IsActive = true };
        return new ShareLocationSnapshotResponse(ToResponse(await _locations.UpsertLatestAsync(snapshot, cancellationToken), now));
    }
    public async Task<GetLocationSnapshotResponse> GetForMonitorAsync(string monitorUserId, string notificationDeliveryAttemptId, CancellationToken cancellationToken)
    {
        User monitor = await GetUserAsync(monitorUserId, UserRole.Monitor, cancellationToken);
        NotificationDeliveryAttempt? attempt = await _attempts.GetByIdAsync(notificationDeliveryAttemptId, cancellationToken);
        if (attempt is null) throw new NotFoundAppException("Alert was not found.");
        IReadOnlyList<EmergencyContact> contacts = await _contacts.GetActiveLinkedByLinkedUserIdAsync(monitor.Id, cancellationToken);
        if (!contacts.Any(c => c.Id == attempt.EmergencyContactId)) throw new NotFoundAppException("Alert was not found.");
        Incident? incident = await _incidents.GetByIdAsync(attempt.IncidentId, cancellationToken);
        if (incident is null || incident.Status != IncidentStatus.Open) throw new LocationNotAvailableAppException("Location is not available.");
        return new GetLocationSnapshotResponse(ToResponse(await GetActiveSnapshotAsync(attempt.IncidentId, cancellationToken), _clock.UtcNow));
    }
    public async Task<GetLocationSnapshotResponse> GetForRiderAsync(string riderUserId, string incidentId, CancellationToken cancellationToken)
    {
        User rider = await GetUserAsync(riderUserId, UserRole.Rider, cancellationToken);
        Incident incident = await GetOwnedIncidentAsync(rider.Id, incidentId, cancellationToken);
        return new GetLocationSnapshotResponse(ToResponse(await GetActiveSnapshotAsync(incident.Id, cancellationToken), _clock.UtcNow));
    }
    private async Task<EmergencyLocationSnapshot> GetActiveSnapshotAsync(string incidentId, CancellationToken cancellationToken) => await _locations.GetActiveByIncidentIdAsync(incidentId, cancellationToken) ?? throw new LocationNotAvailableAppException("Location is not available.");
    private async Task<Incident> GetOwnedIncidentAsync(string userId, string incidentId, CancellationToken ct) { Incident? incident = await _incidents.GetByIdAsync(incidentId.Trim(), ct); if (incident is null || incident.UserId != userId) throw new NotFoundAppException("Incident was not found."); return incident; }
    private async Task EnsureOnboardingReadyAsync(string userId, CancellationToken ct) { OnboardingStatusResponse s = await _onboarding.GetStatusAsync(userId, ct); if (s.CompletedSteps != 7 || s.CurrentStep != "Completed" || !s.IsOperational) throw new OnboardingNotReadyAppException("Onboarding must be completed before sharing location."); }
    private async Task<User> GetUserAsync(string id, UserRole role, CancellationToken ct) { User? u = await _users.GetByIdAsync(id, ct); if (u is null || !u.IsActive) throw new UnauthorizedAppException("Invalid authentication credentials."); if (u.Role != role) throw new ForbiddenAppException("Location Sharing API is not available for this role."); return u; }
    private LocationSnapshotResponse ToResponse(EmergencyLocationSnapshot s, DateTimeOffset now) => new(s.IncidentId, s.TripId, s.Latitude, s.Longitude, s.AccuracyMeters, s.Source.ToString(), s.RecordedAtUtc, s.ReceivedAtUtc, s.IsActive, _staleness.IsStale(s.RecordedAtUtc, now));
    private static T? Parse<T>(string? value) where T : struct => Enum.TryParse(value, true, out T result) ? result : null;
}
