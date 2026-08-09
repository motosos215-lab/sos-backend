using System.Globalization;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AlertAcknowledgements.Application;
using MotoSOS.API.Modules.AlertAcknowledgements.Domain;
using MotoSOS.API.Modules.AlertDispatch.Application;
using MotoSOS.API.Modules.AlertDispatch.Domain;
using MotoSOS.API.Modules.AuditLogs.Application;
using MotoSOS.API.Modules.AuditLogs.Domain;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
using MotoSOS.API.Modules.Escalations.Contracts;
using MotoSOS.API.Modules.Escalations.Domain;
using MotoSOS.API.Modules.Incidents.Application;
using MotoSOS.API.Modules.Incidents.Domain;
using MotoSOS.API.Modules.Notifications.Application;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Modules.Escalations.Application;

public sealed class EmergencyEscalationService : IEmergencyEscalationService
{
    private readonly IUserRepository _users;
    private readonly IAlertDispatchRepository _dispatches;
    private readonly IIncidentRepository _incidents;
    private readonly INotificationDeliveryAttemptRepository _notifications;
    private readonly IAlertAcknowledgementRepository _acknowledgements;
    private readonly IMonitorLinkedContactRepository _contacts;
    private readonly INotificationAttemptMonitorRepository _monitorAttempts;
    private readonly IEmergencyEscalationRepository _escalations;
    private readonly IEmergencyEscalationIdempotencyKeyFactory _keys;
    private readonly IAuditLogService? _auditLogs;
    private readonly IClock _clock;

    public EmergencyEscalationService(IUserRepository users, IAlertDispatchRepository dispatches, IIncidentRepository incidents, INotificationDeliveryAttemptRepository notifications, IAlertAcknowledgementRepository acknowledgements, IMonitorLinkedContactRepository contacts, INotificationAttemptMonitorRepository monitorAttempts, IEmergencyEscalationRepository escalations, IEmergencyEscalationIdempotencyKeyFactory keys, IClock clock, IAuditLogService? auditLogs = null)
    {
        _users = users; _dispatches = dispatches; _incidents = incidents; _notifications = notifications; _acknowledgements = acknowledgements; _contacts = contacts; _monitorAttempts = monitorAttempts; _escalations = escalations; _keys = keys; _clock = clock; _auditLogs = auditLogs;
    }

    public async Task<CreateEmergencyEscalationResponse> EscalateAsync(string riderUserId, string alertDispatchId, CreateEmergencyEscalationRequest request, CancellationToken cancellationToken)
    {
        User rider = await GetUserAsync(riderUserId, UserRole.Rider, cancellationToken);
        AlertDispatchRequest dispatch = await GetOwnedDispatchAsync(rider.Id, alertDispatchId, cancellationToken);
        Incident incident = await GetOpenIncidentAsync(rider.Id, dispatch.IncidentId, cancellationToken);
        EmergencyEscalation? existing = await _escalations.GetByAlertDispatchIdAsync(dispatch.Id, cancellationToken);
        if (existing is not null) return new CreateEmergencyEscalationResponse(ToResponse(existing));

        IReadOnlyList<NotificationDeliveryAttempt> attempts = await _notifications.ListByAlertDispatchIdAsync(rider.Id, dispatch.Id, cancellationToken);
        IReadOnlyList<AlertAcknowledgement> acknowledgements = await _acknowledgements.ListByAlertDispatchIdAsync(rider.Id, dispatch.Id, cancellationToken);
        EmergencyEscalationReason requestedReason = ParseReason(request.Reason);
        EnsureEscalationAllowed(requestedReason, attempts, acknowledgements);

        DateTimeOffset now = _clock.UtcNow;
        var escalation = new EmergencyEscalation
        {
            UserId = rider.Id,
            IncidentId = incident.Id,
            TripId = dispatch.TripId,
            AlertDispatchId = dispatch.Id,
            Status = EmergencyEscalationStatus.Requested,
            Reason = requestedReason,
            Level = ParseLevel(request.Level),
            RequestedByUserId = rider.Id,
            RequestedByRole = rider.Role.ToString(),
            Notes = NormalizeOptional(request.Notes),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            IdempotencyKey = _keys.Create(rider.Id, dispatch.Id)
        };
        ApplyCounts(escalation, attempts, acknowledgements);

        (EmergencyEscalation saved, _) = await _escalations.AddOrGetDuplicateAsync(escalation, cancellationToken);
        await RecordAsync(rider, AuditAction.EmergencyEscalationRequested, saved, cancellationToken);
        return new CreateEmergencyEscalationResponse(ToResponse(saved));
    }

    public async Task<GetEmergencyEscalationResponse> GetForRiderAsync(string riderUserId, string alertDispatchId, CancellationToken cancellationToken)
    {
        User rider = await GetUserAsync(riderUserId, UserRole.Rider, cancellationToken);
        AlertDispatchRequest dispatch = await GetOwnedDispatchAsync(rider.Id, alertDispatchId, cancellationToken);
        return new GetEmergencyEscalationResponse(ToResponse(await GetEscalationAsync(dispatch.Id, cancellationToken)));
    }

    public async Task<GetEmergencyEscalationResponse> GetForMonitorAsync(string monitorUserId, string notificationDeliveryAttemptId, CancellationToken cancellationToken)
    {
        User monitor = await GetUserAsync(monitorUserId, UserRole.Monitor, cancellationToken);
        NotificationDeliveryAttempt attempt = await _monitorAttempts.GetByIdAsync(notificationDeliveryAttemptId.Trim(), cancellationToken) ?? throw new NotFoundAppException("Alert was not found.");
        IReadOnlyList<EmergencyContact> contacts = await _contacts.GetActiveLinkedByLinkedUserIdAsync(monitor.Id, cancellationToken);
        if (!contacts.Any(contact => contact.Id == attempt.EmergencyContactId)) throw new NotFoundAppException("Alert was not found.");
        return new GetEmergencyEscalationResponse(ToResponse(await GetEscalationAsync(attempt.AlertDispatchId, cancellationToken)));
    }

    public async Task<GetEmergencyEscalationResponse> MarkUnresolvedAsync(string riderUserId, string alertDispatchId, CancellationToken cancellationToken)
    {
        User rider = await GetUserAsync(riderUserId, UserRole.Rider, cancellationToken);
        AlertDispatchRequest dispatch = await GetOwnedDispatchAsync(rider.Id, alertDispatchId, cancellationToken);
        EmergencyEscalation escalation = await GetEscalationAsync(dispatch.Id, cancellationToken);
        if (escalation.Status != EmergencyEscalationStatus.Unresolved)
        {
            DateTimeOffset now = _clock.UtcNow;
            escalation.Status = EmergencyEscalationStatus.Unresolved;
            escalation.MarkedUnresolvedAtUtc = now;
            escalation.UpdatedAtUtc = now;
            await _escalations.UpdateAsync(escalation, cancellationToken);
        }
        await RecordAsync(rider, AuditAction.EmergencyEscalationMarkedUnresolved, escalation, cancellationToken);
        return new GetEmergencyEscalationResponse(ToResponse(escalation));
    }

    public async Task<GetEmergencyEscalationResponse> CancelAsync(string riderUserId, string alertDispatchId, CancellationToken cancellationToken)
    {
        User rider = await GetUserAsync(riderUserId, UserRole.Rider, cancellationToken);
        AlertDispatchRequest dispatch = await GetOwnedDispatchAsync(rider.Id, alertDispatchId, cancellationToken);
        EmergencyEscalation escalation = await GetEscalationAsync(dispatch.Id, cancellationToken);
        if (escalation.Status != EmergencyEscalationStatus.Cancelled)
        {
            DateTimeOffset now = _clock.UtcNow;
            escalation.Status = EmergencyEscalationStatus.Cancelled;
            escalation.CancelledAtUtc = now;
            escalation.UpdatedAtUtc = now;
            await _escalations.UpdateAsync(escalation, cancellationToken);
        }
        await RecordAsync(rider, AuditAction.EmergencyEscalationCancelled, escalation, cancellationToken);
        return new GetEmergencyEscalationResponse(ToResponse(escalation));
    }

    public async Task<GetEmergencyEscalationsResponse> ListForAdminAsync(string adminUserId, EmergencyEscalationQuery query, CancellationToken cancellationToken)
    {
        await GetUserAsync(adminUserId, UserRole.Admin, cancellationToken);
        IReadOnlyList<EmergencyEscalation> items = await _escalations.ListAsync(query, cancellationToken);
        long total = await _escalations.CountAsync(query, cancellationToken);
        return new GetEmergencyEscalationsResponse(items.Select(ToResponse).ToArray(), query.PageNumber, query.PageSize, total);
    }

    private static void EnsureEscalationAllowed(EmergencyEscalationReason requestedReason, IReadOnlyList<NotificationDeliveryAttempt> attempts, IReadOnlyList<AlertAcknowledgement> acknowledgements)
    {
        if (acknowledgements.Any(a => a.Status == AlertAcknowledgementStatus.Acknowledged)) throw new EmergencyEscalationNotAllowedAppException("Escalation is not allowed because the alert was already acknowledged. Reason: already_acknowledged.");
        bool hasAttempts = attempts.Count > 0;
        bool hasSimulatedSent = attempts.Any(a => a.Status == NotificationDeliveryStatus.SimulatedSent);
        bool allDeclined = acknowledgements.Count > 0 && acknowledgements.All(a => a.Status == AlertAcknowledgementStatus.Declined);

        bool allowed = requestedReason switch
        {
            EmergencyEscalationReason.ManualEscalation => true,
            EmergencyEscalationReason.NoAcknowledgement => hasAttempts && hasSimulatedSent,
            EmergencyEscalationReason.AllContactsDeclined => allDeclined,
            EmergencyEscalationReason.SimulatedEmergencyFollowUp => hasAttempts && hasSimulatedSent,
            _ => false
        };
        if (!allowed) throw new EmergencyEscalationNotAllowedAppException("Escalation reason does not match the current emergency state.");
    }

    private async Task<AlertDispatchRequest> GetOwnedDispatchAsync(string userId, string alertDispatchId, CancellationToken cancellationToken)
    {
        AlertDispatchRequest? dispatch = await _dispatches.GetByIdAsync(alertDispatchId.Trim(), cancellationToken);
        if (dispatch is null || dispatch.UserId != userId) throw new NotFoundAppException("Alert dispatch was not found.");
        return dispatch;
    }

    private async Task<Incident> GetOpenIncidentAsync(string userId, string incidentId, CancellationToken cancellationToken)
    {
        Incident? incident = await _incidents.GetByIdAsync(incidentId, cancellationToken);
        if (incident is null || incident.UserId != userId) throw new NotFoundAppException("Incident was not found.");
        if (incident.Status is IncidentStatus.Closed or IncidentStatus.FalsePositiveCancelled) throw new IncidentNotReadyAppException("Closed or cancelled incidents cannot be escalated.");
        if (incident.Status != IncidentStatus.Open) throw new IncidentNotReadyAppException("Incident is not ready for escalation.");
        return incident;
    }

    private async Task<EmergencyEscalation> GetEscalationAsync(string alertDispatchId, CancellationToken cancellationToken) => await _escalations.GetByAlertDispatchIdAsync(alertDispatchId.Trim(), cancellationToken) ?? throw new EmergencyEscalationNotAvailableAppException("Emergency escalation is not available.");

    private async Task<User> GetUserAsync(string userId, UserRole role, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive) throw new UnauthorizedAppException("Invalid authentication credentials.");
        if (user.Role != role) throw new ForbiddenAppException("Emergency Escalation API is not available for this role.");
        return user;
    }

    private static void ApplyCounts(EmergencyEscalation e, IReadOnlyList<NotificationDeliveryAttempt> attempts, IReadOnlyList<AlertAcknowledgement> acks)
    {
        e.NotificationsTotal = attempts.Count;
        e.NotificationsPrepared = attempts.Count(a => a.Status == NotificationDeliveryStatus.Prepared);
        e.NotificationsSimulatedSent = attempts.Count(a => a.Status == NotificationDeliveryStatus.SimulatedSent);
        e.NotificationsFailed = attempts.Count(a => a.Status == NotificationDeliveryStatus.Failed);
        e.NotificationsCancelled = attempts.Count(a => a.Status == NotificationDeliveryStatus.Cancelled);
        e.AcknowledgementsTotal = acks.Count;
        e.AcknowledgedCount = acks.Count(a => a.Status == AlertAcknowledgementStatus.Acknowledged);
        e.DeclinedCount = acks.Count(a => a.Status == AlertAcknowledgementStatus.Declined);
        e.ViewedCount = acks.Count(a => a.Status == AlertAcknowledgementStatus.Viewed);
        e.PendingCount = acks.Count(a => a.Status == AlertAcknowledgementStatus.Pending);
    }

    private async Task RecordAsync(User actor, AuditAction action, EmergencyEscalation escalation, CancellationToken cancellationToken)
    {
        try
        {
            if (_auditLogs is not null) await _auditLogs.RecordAsync(actor.Id, actor.Role.ToString(), action, AuditModule.AlertDispatch, "EmergencyEscalation", escalation.Id, AuditOutcome.Success, null, null, null, new Dictionary<string, string> { ["escalationId"] = escalation.Id, ["incidentId"] = escalation.IncidentId, ["alertDispatchId"] = escalation.AlertDispatchId, ["reason"] = escalation.Reason.ToString(), ["level"] = escalation.Level.ToString(), ["status"] = escalation.Status.ToString(), ["notificationsTotal"] = escalation.NotificationsTotal.ToString(CultureInfo.InvariantCulture), ["acknowledgedCount"] = escalation.AcknowledgedCount.ToString(CultureInfo.InvariantCulture), ["declinedCount"] = escalation.DeclinedCount.ToString(CultureInfo.InvariantCulture) }, cancellationToken);
        }
        catch
        {
        }
    }
    private static EmergencyEscalationReason ParseReason(string? value) => Enum.Parse<EmergencyEscalationReason>(value!, false);
    private static EmergencyEscalationLevel ParseLevel(string? value) => Enum.Parse<EmergencyEscalationLevel>(value!, false);
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static EmergencyEscalationResponse ToResponse(EmergencyEscalation e) => new(e.Id, e.IncidentId, e.TripId, e.AlertDispatchId, e.Status.ToString(), e.Reason.ToString(), e.Level.ToString(), e.Notes, e.NotificationsTotal, e.NotificationsPrepared, e.NotificationsSimulatedSent, e.NotificationsFailed, e.NotificationsCancelled, e.AcknowledgementsTotal, e.AcknowledgedCount, e.DeclinedCount, e.ViewedCount, e.PendingCount, e.CreatedAtUtc, e.UpdatedAtUtc, e.ResolvedAtUtc, e.MarkedUnresolvedAtUtc, e.CancelledAtUtc);
}
