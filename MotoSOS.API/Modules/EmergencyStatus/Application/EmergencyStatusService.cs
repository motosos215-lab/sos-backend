using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AlertAcknowledgements.Application;
using MotoSOS.API.Modules.AlertAcknowledgements.Domain;
using MotoSOS.API.Modules.AlertDispatch.Application;
using MotoSOS.API.Modules.AlertDispatch.Domain;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
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

namespace MotoSOS.API.Modules.EmergencyStatus.Application;

public sealed class EmergencyStatusService : IEmergencyStatusService
{
    private const int DefaultPageNumber = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly IUserRepository _users;
    private readonly IIncidentRepository _incidents;
    private readonly ITripRepository _trips;
    private readonly IAlertDispatchRepository _alertDispatches;
    private readonly INotificationDeliveryAttemptRepository _notifications;
    private readonly IAlertAcknowledgementRepository _acknowledgements;
    private readonly ILocationSharingRepository _locations;
    private readonly IMonitorLinkedContactRepository _contacts;
    private readonly INotificationAttemptMonitorRepository _monitorAttempts;
    private readonly ILocationSharingStalenessService _staleness;
    private readonly IClock _clock;

    public EmergencyStatusService(IUserRepository users, IIncidentRepository incidents, ITripRepository trips, IAlertDispatchRepository alertDispatches, INotificationDeliveryAttemptRepository notifications, IAlertAcknowledgementRepository acknowledgements, ILocationSharingRepository locations, IMonitorLinkedContactRepository contacts, INotificationAttemptMonitorRepository monitorAttempts, ILocationSharingStalenessService staleness, IClock clock)
    {
        _users = users; _incidents = incidents; _trips = trips; _alertDispatches = alertDispatches; _notifications = notifications; _acknowledgements = acknowledgements; _locations = locations; _contacts = contacts; _monitorAttempts = monitorAttempts; _staleness = staleness; _clock = clock;
    }

    public async Task<EmergencyStatusResponse> GetForRiderAsync(string riderUserId, string incidentId, CancellationToken cancellationToken)
    {
        User rider = await GetUserAsync(riderUserId, UserRole.Rider, cancellationToken);
        Incident incident = await GetOwnedIncidentAsync(rider.Id, incidentId, cancellationToken);
        return await BuildAsync(incident, cancellationToken);
    }

    public async Task<EmergencyStatusResponse> GetForMonitorAsync(string monitorUserId, string notificationDeliveryAttemptId, CancellationToken cancellationToken)
    {
        User monitor = await GetUserAsync(monitorUserId, UserRole.Monitor, cancellationToken);
        NotificationDeliveryAttempt? attempt = await _monitorAttempts.GetByIdAsync(notificationDeliveryAttemptId.Trim(), cancellationToken);
        if (attempt is null) throw new NotFoundAppException("Alert was not found.");
        IReadOnlyList<EmergencyContact> contacts = await _contacts.GetActiveLinkedByLinkedUserIdAsync(monitor.Id, cancellationToken);
        if (!contacts.Any(contact => contact.Id == attempt.EmergencyContactId)) throw new NotFoundAppException("Alert was not found.");
        Incident? incident = await _incidents.GetByIdAsync(attempt.IncidentId, cancellationToken);
        if (incident is null) throw new EmergencyStatusNotAvailableAppException("Emergency status is not available.");
        return await BuildAsync(incident, cancellationToken);
    }

    public async Task<GetActiveEmergenciesResponse> ListActiveForRiderAsync(string riderUserId, int? pageNumber, int? pageSize, CancellationToken cancellationToken)
    {
        User rider = await GetUserAsync(riderUserId, UserRole.Rider, cancellationToken);
        int page = Math.Max(pageNumber ?? DefaultPageNumber, 1);
        int size = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        IReadOnlyList<Incident> incidents = await _incidents.ListByUserIdAsync(rider.Id, IncidentStatus.Open, null, page, size, cancellationToken);
        long total = await _incidents.CountByUserIdAsync(rider.Id, IncidentStatus.Open, null, cancellationToken);
        var summaries = new List<EmergencyStatusResponse>(incidents.Count);
        foreach (Incident incident in incidents)
        {
            summaries.Add(await BuildAsync(incident, cancellationToken));
        }

        return new GetActiveEmergenciesResponse(summaries, page, size, total);
    }

    private async Task<EmergencyStatusResponse> BuildAsync(Incident incident, CancellationToken cancellationToken)
    {
        Trip? trip = string.IsNullOrWhiteSpace(incident.TripId) ? null : await _trips.GetByIdAsync(incident.TripId, cancellationToken);
        IReadOnlyList<AlertDispatchRequest> dispatches = await _alertDispatches.ListByIncidentIdAsync(incident.UserId, incident.Id, cancellationToken);
        AlertDispatchRequest? dispatch = dispatches.OrderByDescending(d => d.CreatedAtUtc).FirstOrDefault();
        IReadOnlyList<NotificationDeliveryAttempt> notifications = dispatch is not null
            ? await _notifications.ListByAlertDispatchIdAsync(incident.UserId, dispatch.Id, cancellationToken)
            : await _notifications.ListByIncidentIdAsync(incident.UserId, incident.Id, cancellationToken);
        IReadOnlyList<AlertAcknowledgement> acknowledgements = dispatch is not null
            ? await _acknowledgements.ListByAlertDispatchIdAsync(incident.UserId, dispatch.Id, cancellationToken)
            : await _acknowledgements.ListByIncidentIdAsync(incident.UserId, incident.Id, cancellationToken);
        EmergencyLocationSnapshot? location = await _locations.GetActiveByIncidentIdAsync(incident.Id, cancellationToken);
        EmergencyOverallStatus overall = CalculateOverallStatus(incident.Status, notifications, acknowledgements);
        bool requiresAttention = incident.Status == IncidentStatus.Open && !acknowledgements.Any(a => a.Status == AlertAcknowledgementStatus.Acknowledged);

        return new EmergencyStatusResponse(
            ToIncident(incident),
            trip is null ? null : new EmergencyTripStatusResponse(trip.Id, trip.Status.ToString(), trip.StartedAtUtc, trip.FinishedAtUtc),
            dispatch is null ? null : new EmergencyAlertDispatchStatusResponse(dispatch.Id, dispatch.Status.ToString(), dispatch.Priority.ToString(), dispatch.Reason.ToString(), dispatch.CreatedAtUtc),
            ToNotifications(notifications),
            ToAcknowledgements(acknowledgements),
            ToLocation(location),
            overall.ToString(),
            requiresAttention,
            LastUpdated(incident, trip, dispatch, notifications, acknowledgements, location));
    }

    private async Task<User> GetUserAsync(string userId, UserRole role, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive) throw new UnauthorizedAppException("Invalid authentication credentials.");
        if (user.Role != role) throw new ForbiddenAppException("Emergency Status API is not available for this role.");
        return user;
    }

    private async Task<Incident> GetOwnedIncidentAsync(string userId, string incidentId, CancellationToken cancellationToken)
    {
        Incident? incident = await _incidents.GetByIdAsync(incidentId.Trim(), cancellationToken);
        if (incident is null || incident.UserId != userId) throw new NotFoundAppException("Incident was not found.");
        return incident;
    }

    private static EmergencyIncidentStatusResponse ToIncident(Incident incident) => new(incident.Id, incident.Status.ToString(), incident.Source.ToString(), incident.Cause.ToString(), incident.RiskLevel.ToString(), incident.OccurredAtUtc, incident.CreatedAtUtc);
    private static EmergencyNotificationSummaryResponse ToNotifications(IReadOnlyList<NotificationDeliveryAttempt> attempts) => new(attempts.Count, attempts.Count(a => a.Status == NotificationDeliveryStatus.Prepared), attempts.Count(a => a.Status == NotificationDeliveryStatus.SimulatedSent), attempts.Count(a => a.Status == NotificationDeliveryStatus.Failed), attempts.Count(a => a.Status == NotificationDeliveryStatus.Cancelled));
    private static EmergencyAcknowledgementSummaryResponse ToAcknowledgements(IReadOnlyList<AlertAcknowledgement> acknowledgements) => new(acknowledgements.Count, acknowledgements.Count(a => a.Status == AlertAcknowledgementStatus.Pending), acknowledgements.Count(a => a.Status == AlertAcknowledgementStatus.Viewed), acknowledgements.Count(a => a.Status == AlertAcknowledgementStatus.Acknowledged), acknowledgements.Count(a => a.Status == AlertAcknowledgementStatus.Declined));
    private EmergencyLocationStatusResponse ToLocation(EmergencyLocationSnapshot? location) => location is null ? new EmergencyLocationStatusResponse(false, null, null, null, null, null, null, null, null, null, null) : new EmergencyLocationStatusResponse(true, location.IncidentId, location.TripId, location.Latitude, location.Longitude, location.AccuracyMeters, location.Source.ToString(), location.RecordedAtUtc, location.ReceivedAtUtc, location.IsActive, _staleness.IsStale(location.RecordedAtUtc, _clock.UtcNow));

    private static EmergencyOverallStatus CalculateOverallStatus(IncidentStatus incidentStatus, IReadOnlyList<NotificationDeliveryAttempt> notifications, IReadOnlyList<AlertAcknowledgement> acknowledgements)
    {
        if (incidentStatus == IncidentStatus.Closed) return EmergencyOverallStatus.Closed;
        if (incidentStatus == IncidentStatus.FalsePositiveCancelled) return EmergencyOverallStatus.Cancelled;
        if (acknowledgements.Any(a => a.Status == AlertAcknowledgementStatus.Acknowledged)) return EmergencyOverallStatus.Acknowledged;
        if (acknowledgements.Count > 0 && acknowledgements.All(a => a.Status == AlertAcknowledgementStatus.Declined)) return EmergencyOverallStatus.Declined;
        if (notifications.Count > 0) return EmergencyOverallStatus.AwaitingAcknowledgement;
        if (incidentStatus == IncidentStatus.Open) return EmergencyOverallStatus.Active;
        return EmergencyOverallStatus.Unknown;
    }

    private static DateTimeOffset LastUpdated(Incident incident, Trip? trip, AlertDispatchRequest? dispatch, IReadOnlyList<NotificationDeliveryAttempt> notifications, IReadOnlyList<AlertAcknowledgement> acknowledgements, EmergencyLocationSnapshot? location)
    {
        IEnumerable<DateTimeOffset?> values = [incident.UpdatedAtUtc ?? incident.CreatedAtUtc, trip?.UpdatedAtUtc ?? trip?.CreatedAtUtc, dispatch?.UpdatedAtUtc ?? dispatch?.CreatedAtUtc, location?.UpdatedAtUtc ?? location?.ReceivedAtUtc];
        values = values.Concat(notifications.Select(a => (DateTimeOffset?)(a.UpdatedAtUtc ?? a.LastStatusChangedAtUtc ?? a.CreatedAtUtc)));
        values = values.Concat(acknowledgements.Select(a => (DateTimeOffset?)(a.UpdatedAtUtc ?? a.AcknowledgedAtUtc ?? a.DeclinedAtUtc ?? a.ViewedAtUtc ?? a.CreatedAtUtc)));
        return values.Where(value => value.HasValue).Select(value => value!.Value).DefaultIfEmpty(incident.CreatedAtUtc).Max();
    }
}
