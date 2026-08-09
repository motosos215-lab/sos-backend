using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AlertAcknowledgements.Application;
using MotoSOS.API.Modules.AlertAcknowledgements.Domain;
using MotoSOS.API.Modules.AlertDispatch.Application;
using MotoSOS.API.Modules.AlertDispatch.Domain;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
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

namespace MotoSOS.API.Modules.EmergencyResolution.Application;

public sealed class EmergencyResolutionService : IEmergencyResolutionService
{
    private const int DefaultPageNumber = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly IUserRepository _users;
    private readonly IIncidentRepository _incidents;
    private readonly IAlertDispatchRepository _alertDispatches;
    private readonly INotificationDeliveryAttemptRepository _notifications;
    private readonly IAlertAcknowledgementRepository _acknowledgements;
    private readonly ILocationSharingRepository _locations;
    private readonly IMonitorLinkedContactRepository _contacts;
    private readonly INotificationAttemptMonitorRepository _monitorAttempts;
    private readonly IEmergencyResolutionRepository _reports;
    private readonly IEmergencyResolutionIdempotencyKeyFactory _idempotencyKeys;
    private readonly ILocationSharingStalenessService _staleness;
    private readonly IClock _clock;

    public EmergencyResolutionService(IUserRepository users, IIncidentRepository incidents, IAlertDispatchRepository alertDispatches, INotificationDeliveryAttemptRepository notifications, IAlertAcknowledgementRepository acknowledgements, ILocationSharingRepository locations, IMonitorLinkedContactRepository contacts, INotificationAttemptMonitorRepository monitorAttempts, IEmergencyResolutionRepository reports, IEmergencyResolutionIdempotencyKeyFactory idempotencyKeys, ILocationSharingStalenessService staleness, IClock clock)
    {
        _users = users; _incidents = incidents; _alertDispatches = alertDispatches; _notifications = notifications; _acknowledgements = acknowledgements; _locations = locations; _contacts = contacts; _monitorAttempts = monitorAttempts; _reports = reports; _idempotencyKeys = idempotencyKeys; _staleness = staleness; _clock = clock;
    }

    public async Task<CreateEmergencyResolutionReportResponse> CreateForRiderAsync(string riderUserId, string incidentId, CreateEmergencyResolutionReportRequest request, CancellationToken cancellationToken)
    {
        User rider = await GetUserAsync(riderUserId, UserRole.Rider, cancellationToken);
        Incident incident = await GetOwnedIncidentAsync(rider.Id, incidentId, cancellationToken);
        if (incident.Status == IncidentStatus.Open) throw new IncidentNotReadyAppException("Emergency resolution report can be created only after the incident is closed or cancelled.");
        if (incident.Status is not (IncidentStatus.Closed or IncidentStatus.FalsePositiveCancelled)) throw new EmergencyResolutionNotAllowedAppException("Emergency resolution report is not allowed for this incident status.");

        string idempotencyKey = _idempotencyKeys.Create(rider.Id, incident.Id);
        EmergencyResolutionReport? existing = await _reports.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken) ?? await _reports.GetByIncidentIdAsync(incident.Id, cancellationToken);
        if (existing is not null) return new CreateEmergencyResolutionReportResponse(ToResponse(existing), true);

        DateTimeOffset now = _clock.UtcNow;
        EmergencyResolutionOutcome outcome = ParseOutcome(request.Outcome);
        (AlertDispatchRequest? dispatch, IReadOnlyList<NotificationDeliveryAttempt> notifications, IReadOnlyList<AlertAcknowledgement> acknowledgements) = await LoadAggregatesAsync(incident, cancellationToken);
        EmergencyLocationSnapshot? location = await _locations.GetLatestByIncidentIdAsync(incident.Id, cancellationToken);
        DateTimeOffset? firstAcknowledgedAtUtc = acknowledgements.Where(a => a.AcknowledgedAtUtc.HasValue).Select(a => a.AcknowledgedAtUtc).MinBy(value => value);
        DateTimeOffset incidentClosedAtUtc = GetIncidentClosedAtUtc(incident);

        var report = new EmergencyResolutionReport
        {
            UserId = rider.Id,
            IncidentId = incident.Id,
            TripId = incident.TripId,
            AlertDispatchId = dispatch?.Id,
            IdempotencyKey = idempotencyKey,
            Outcome = outcome,
            ClosedByRole = GetClosedByRole(incident),
            Summary = request.Summary!.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            IncidentCreatedAtUtc = incident.CreatedAtUtc,
            IncidentClosedAtUtc = incidentClosedAtUtc,
            NotificationAttemptsTotal = notifications.Count,
            AcknowledgementsTotal = acknowledgements.Count,
            AcknowledgedCount = acknowledgements.Count(a => a.Status == AlertAcknowledgementStatus.Acknowledged),
            DeclinedCount = acknowledgements.Count(a => a.Status == AlertAcknowledgementStatus.Declined),
            FirstNotificationPreparedAtUtc = notifications.Select(a => (DateTimeOffset?)a.PreparedAtUtc).DefaultIfEmpty().Min(),
            FirstAcknowledgedAtUtc = firstAcknowledgedAtUtc,
            ResponseTimeSeconds = firstAcknowledgedAtUtc.HasValue ? Math.Max(0, (long)(firstAcknowledgedAtUtc.Value - incident.CreatedAtUtc).TotalSeconds) : null,
            FinalLatitude = location?.Latitude,
            FinalLongitude = location?.Longitude,
            FinalLocationRecordedAtUtc = location?.RecordedAtUtc,
            LastKnownLocationWasStale = location is null ? null : _staleness.IsStale(location.RecordedAtUtc, now),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        (EmergencyResolutionReport saved, bool isDuplicate) = await _reports.AddOrGetDuplicateAsync(report, cancellationToken);
        return new CreateEmergencyResolutionReportResponse(ToResponse(saved), isDuplicate);
    }

    public async Task<GetEmergencyResolutionReportResponse> GetForRiderAsync(string riderUserId, string incidentId, CancellationToken cancellationToken)
    {
        User rider = await GetUserAsync(riderUserId, UserRole.Rider, cancellationToken);
        Incident incident = await GetOwnedIncidentAsync(rider.Id, incidentId, cancellationToken);
        EmergencyResolutionReport report = await GetReportAsync(incident.Id, cancellationToken);
        if (report.UserId != rider.Id) throw new NotFoundAppException("Incident was not found.");
        return new GetEmergencyResolutionReportResponse(ToResponse(report));
    }

    public async Task<GetEmergencyResolutionReportResponse> GetForMonitorAsync(string monitorUserId, string notificationDeliveryAttemptId, CancellationToken cancellationToken)
    {
        User monitor = await GetUserAsync(monitorUserId, UserRole.Monitor, cancellationToken);
        NotificationDeliveryAttempt? attempt = await _monitorAttempts.GetByIdAsync(notificationDeliveryAttemptId.Trim(), cancellationToken);
        if (attempt is null) throw new NotFoundAppException("Alert was not found.");
        IReadOnlyList<EmergencyContact> contacts = await _contacts.GetActiveLinkedByLinkedUserIdAsync(monitor.Id, cancellationToken);
        if (!contacts.Any(contact => contact.Id == attempt.EmergencyContactId)) throw new NotFoundAppException("Alert was not found.");
        EmergencyResolutionReport report = await GetReportAsync(attempt.IncidentId, cancellationToken);
        return new GetEmergencyResolutionReportResponse(ToResponse(report));
    }

    public async Task<GetEmergencyResolutionReportsResponse> ListForRiderAsync(string riderUserId, string? outcome, int? pageNumber, int? pageSize, CancellationToken cancellationToken)
    {
        User rider = await GetUserAsync(riderUserId, UserRole.Rider, cancellationToken);
        int page = Math.Max(pageNumber ?? DefaultPageNumber, 1);
        int size = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        EmergencyResolutionOutcome? parsedOutcome = string.IsNullOrWhiteSpace(outcome) ? null : ParseOutcome(outcome);
        IReadOnlyList<EmergencyResolutionReport> reports = await _reports.ListByUserIdAsync(rider.Id, parsedOutcome, page, size, cancellationToken);
        long total = await _reports.CountByUserIdAsync(rider.Id, parsedOutcome, cancellationToken);
        return new GetEmergencyResolutionReportsResponse(reports.Select(ToResponse).ToArray(), page, size, total);
    }

    private async Task<(AlertDispatchRequest? Dispatch, IReadOnlyList<NotificationDeliveryAttempt> Notifications, IReadOnlyList<AlertAcknowledgement> Acknowledgements)> LoadAggregatesAsync(Incident incident, CancellationToken cancellationToken)
    {
        IReadOnlyList<AlertDispatchRequest> dispatches = await _alertDispatches.ListByIncidentIdAsync(incident.UserId, incident.Id, cancellationToken);
        AlertDispatchRequest? dispatch = dispatches.OrderByDescending(d => d.CreatedAtUtc).FirstOrDefault();
        IReadOnlyList<NotificationDeliveryAttempt> notifications = dispatch is not null ? await _notifications.ListByAlertDispatchIdAsync(incident.UserId, dispatch.Id, cancellationToken) : await _notifications.ListByIncidentIdAsync(incident.UserId, incident.Id, cancellationToken);
        IReadOnlyList<AlertAcknowledgement> acknowledgements = dispatch is not null ? await _acknowledgements.ListByAlertDispatchIdAsync(incident.UserId, dispatch.Id, cancellationToken) : await _acknowledgements.ListByIncidentIdAsync(incident.UserId, incident.Id, cancellationToken);
        return (dispatch, notifications, acknowledgements);
    }

    private async Task<User> GetUserAsync(string userId, UserRole role, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive) throw new UnauthorizedAppException("Invalid authentication credentials.");
        if (user.Role != role) throw new ForbiddenAppException("Emergency Resolution API is not available for this role.");
        return user;
    }

    private async Task<Incident> GetOwnedIncidentAsync(string userId, string incidentId, CancellationToken cancellationToken)
    {
        Incident? incident = await _incidents.GetByIdAsync(incidentId.Trim(), cancellationToken);
        if (incident is null || incident.UserId != userId) throw new NotFoundAppException("Incident was not found.");
        return incident;
    }

    private async Task<EmergencyResolutionReport> GetReportAsync(string incidentId, CancellationToken cancellationToken) => await _reports.GetByIncidentIdAsync(incidentId.Trim(), cancellationToken) ?? throw new EmergencyResolutionNotAvailableAppException("Emergency resolution report is not available.");

    private static EmergencyResolutionOutcome ParseOutcome(string? outcome)
    {
        if (Enum.TryParse(outcome, true, out EmergencyResolutionOutcome parsed) && parsed != EmergencyResolutionOutcome.Unknown) return parsed;
        throw new ValidationAppException("Outcome is invalid.");
    }

    private static DateTimeOffset GetIncidentClosedAtUtc(Incident incident) => incident.Status == IncidentStatus.FalsePositiveCancelled ? incident.CancelledAtUtc ?? incident.UpdatedAtUtc ?? incident.CreatedAtUtc : incident.ClosedAtUtc ?? incident.UpdatedAtUtc ?? incident.CreatedAtUtc;
    private static EmergencyClosedByRole GetClosedByRole(Incident incident) => incident.ClosedByUserId == incident.UserId ? EmergencyClosedByRole.Rider : string.IsNullOrWhiteSpace(incident.ClosedByUserId) ? EmergencyClosedByRole.Unknown : EmergencyClosedByRole.Monitor;
    private static EmergencyResolutionReportResponse ToResponse(EmergencyResolutionReport report) => new(report.Id, report.UserId, report.IncidentId, report.TripId, report.AlertDispatchId, report.Outcome.ToString(), report.ClosedByRole.ToString(), report.Summary, report.Notes, report.IncidentCreatedAtUtc, report.IncidentClosedAtUtc, report.NotificationAttemptsTotal, report.AcknowledgementsTotal, report.AcknowledgedCount, report.DeclinedCount, report.FirstNotificationPreparedAtUtc, report.FirstAcknowledgedAtUtc, report.ResponseTimeSeconds, report.FinalLatitude, report.FinalLongitude, report.FinalLocationRecordedAtUtc, report.LastKnownLocationWasStale, report.CreatedAtUtc, report.UpdatedAtUtc);
}
