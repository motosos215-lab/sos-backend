using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AlertAcknowledgements.Contracts;
using MotoSOS.API.Modules.AlertAcknowledgements.Domain;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Modules.AlertAcknowledgements.Application;

public sealed class AlertAcknowledgementService : IAlertAcknowledgementService
{
    private const int DefaultPageNumber = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;
    private readonly IUserRepository _users;
    private readonly IMonitorLinkedContactRepository _contacts;
    private readonly INotificationAttemptMonitorRepository _attempts;
    private readonly IAlertAcknowledgementRepository _acknowledgements;
    private readonly IAlertAcknowledgementIdempotencyKeyFactory _keys;
    private readonly IClock _clock;

    public AlertAcknowledgementService(IUserRepository users, IMonitorLinkedContactRepository contacts, INotificationAttemptMonitorRepository attempts, IAlertAcknowledgementRepository acknowledgements, IAlertAcknowledgementIdempotencyKeyFactory keys, IClock clock)
    {
        _users = users;
        _contacts = contacts;
        _attempts = attempts;
        _acknowledgements = acknowledgements;
        _keys = keys;
        _clock = clock;
    }

    public async Task<GetMonitorAlertsResponse> ListMonitorAlertsAsync(string monitorUserId, string? status, int? pageNumber, int? pageSize, CancellationToken cancellationToken)
    {
        User monitor = await GetUserAsync(monitorUserId, UserRole.Monitor, cancellationToken);
        IReadOnlyList<string> contactIds = await GetLinkedContactIdsAsync(monitor.Id, cancellationToken);
        int normalizedPageNumber = Math.Max(pageNumber ?? DefaultPageNumber, 1);
        int normalizedPageSize = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        AlertAcknowledgementStatus? parsedStatus = ParseEnum<AlertAcknowledgementStatus>(status);
        IReadOnlyList<AlertAcknowledgement> existing = await _acknowledgements.ListByMonitorUserIdAsync(monitor.Id, parsedStatus, normalizedPageNumber, normalizedPageSize, cancellationToken);
        if (parsedStatus.HasValue) return new GetMonitorAlertsResponse(existing.Select(ToResponse).ToArray(), normalizedPageNumber, normalizedPageSize, await _acknowledgements.CountByMonitorUserIdAsync(monitor.Id, parsedStatus, cancellationToken));

        IReadOnlyList<NotificationDeliveryAttempt> attempts = await _attempts.ListByEmergencyContactIdsAsync(contactIds, normalizedPageNumber, normalizedPageSize, cancellationToken);
        var ensured = new List<AlertAcknowledgement>();
        foreach (NotificationDeliveryAttempt attempt in attempts) ensured.Add(await EnsureAcknowledgementAsync(monitor.Id, attempt, cancellationToken));
        return new GetMonitorAlertsResponse(ensured.Select(ToResponse).ToArray(), normalizedPageNumber, normalizedPageSize, await _attempts.CountByEmergencyContactIdsAsync(contactIds, cancellationToken));
    }

    public async Task<ViewAlertResponse> GetMonitorAlertAsync(string monitorUserId, string notificationDeliveryAttemptId, CancellationToken cancellationToken)
    {
        User monitor = await GetUserAsync(monitorUserId, UserRole.Monitor, cancellationToken);
        NotificationDeliveryAttempt attempt = await GetAssignedAttemptAsync(monitor.Id, notificationDeliveryAttemptId, cancellationToken);
        return new ViewAlertResponse(ToResponse(await EnsureAcknowledgementAsync(monitor.Id, attempt, cancellationToken)));
    }

    public async Task<ViewAlertResponse> ViewAsync(string monitorUserId, string notificationDeliveryAttemptId, CancellationToken cancellationToken)
    {
        AlertAcknowledgement ack = await GetOrCreateAssignedAcknowledgementAsync(monitorUserId, notificationDeliveryAttemptId, cancellationToken);
        if (ack.Status == AlertAcknowledgementStatus.Pending)
        {
            DateTimeOffset now = _clock.UtcNow;
            ack.Status = AlertAcknowledgementStatus.Viewed;
            ack.ViewedAtUtc = now;
            ack.UpdatedAtUtc = now;
            await _acknowledgements.UpdateAsync(ack, cancellationToken);
        }
        return new ViewAlertResponse(ToResponse(ack));
    }

    public async Task<AcknowledgeAlertResponse> AcknowledgeAsync(string monitorUserId, string notificationDeliveryAttemptId, AcknowledgeAlertRequest request, CancellationToken cancellationToken)
    {
        AlertAcknowledgement ack = await GetOrCreateAssignedAcknowledgementAsync(monitorUserId, notificationDeliveryAttemptId, cancellationToken);
        if (ack.Status == AlertAcknowledgementStatus.Declined) throw new AcknowledgementAlreadyDeclinedAppException("Acknowledgement was already declined.");
        if (ack.Status != AlertAcknowledgementStatus.Acknowledged)
        {
            DateTimeOffset now = _clock.UtcNow;
            ack.Status = AlertAcknowledgementStatus.Acknowledged;
            ack.ResponseType = ParseEnum<AlertAcknowledgementResponseType>(request.ResponseType)!.Value;
            ack.Message = NormalizeOptional(request.Message);
            ack.AcknowledgedAtUtc = now;
            ack.UpdatedAtUtc = now;
            await _acknowledgements.UpdateAsync(ack, cancellationToken);
        }
        return new AcknowledgeAlertResponse(ToResponse(ack));
    }

    public async Task<DeclineAlertResponse> DeclineAsync(string monitorUserId, string notificationDeliveryAttemptId, DeclineAlertRequest request, CancellationToken cancellationToken)
    {
        AlertAcknowledgement ack = await GetOrCreateAssignedAcknowledgementAsync(monitorUserId, notificationDeliveryAttemptId, cancellationToken);
        if (ack.Status == AlertAcknowledgementStatus.Acknowledged) throw new AcknowledgementAlreadyConfirmedAppException("Acknowledgement was already confirmed.");
        if (ack.Status != AlertAcknowledgementStatus.Declined)
        {
            DateTimeOffset now = _clock.UtcNow;
            ack.Status = AlertAcknowledgementStatus.Declined;
            ack.ResponseType = ParseEnum<AlertAcknowledgementResponseType>(request.ResponseType)!.Value;
            ack.Message = NormalizeOptional(request.Message);
            ack.DeclinedAtUtc = now;
            ack.UpdatedAtUtc = now;
            await _acknowledgements.UpdateAsync(ack, cancellationToken);
        }
        return new DeclineAlertResponse(ToResponse(ack));
    }

    public async Task<GetAlertAcknowledgementsResponse> ListRiderAcknowledgementsAsync(string riderUserId, string? alertDispatchId, string? incidentId, string? status, int? pageNumber, int? pageSize, CancellationToken cancellationToken)
    {
        User rider = await GetUserAsync(riderUserId, UserRole.Rider, cancellationToken);
        int normalizedPageNumber = Math.Max(pageNumber ?? DefaultPageNumber, 1);
        int normalizedPageSize = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        AlertAcknowledgementStatus? parsedStatus = ParseEnum<AlertAcknowledgementStatus>(status);
        IReadOnlyList<AlertAcknowledgement> acks = await _acknowledgements.ListByUserIdAsync(rider.Id, NormalizeOptional(alertDispatchId), NormalizeOptional(incidentId), parsedStatus, normalizedPageNumber, normalizedPageSize, cancellationToken);
        long total = await _acknowledgements.CountByUserIdAsync(rider.Id, NormalizeOptional(alertDispatchId), NormalizeOptional(incidentId), parsedStatus, cancellationToken);
        return new GetAlertAcknowledgementsResponse(acks.Select(ToResponse).ToArray(), normalizedPageNumber, normalizedPageSize, total);
    }

    private async Task<AlertAcknowledgement> GetOrCreateAssignedAcknowledgementAsync(string monitorUserId, string attemptId, CancellationToken cancellationToken)
    {
        User monitor = await GetUserAsync(monitorUserId, UserRole.Monitor, cancellationToken);
        NotificationDeliveryAttempt attempt = await GetAssignedAttemptAsync(monitor.Id, attemptId, cancellationToken);
        return await EnsureAcknowledgementAsync(monitor.Id, attempt, cancellationToken);
    }

    private async Task<NotificationDeliveryAttempt> GetAssignedAttemptAsync(string monitorUserId, string attemptId, CancellationToken cancellationToken)
    {
        NotificationDeliveryAttempt? attempt = await _attempts.GetByIdAsync(attemptId, cancellationToken);
        if (attempt is null) throw new NotFoundAppException("Alert was not found.");
        IReadOnlyList<string> contactIds = await GetLinkedContactIdsAsync(monitorUserId, cancellationToken);
        if (!contactIds.Contains(attempt.EmergencyContactId, StringComparer.Ordinal)) throw new NotFoundAppException("Alert was not found.");
        return attempt;
    }

    private async Task<IReadOnlyList<string>> GetLinkedContactIdsAsync(string monitorUserId, CancellationToken cancellationToken)
    {
        IReadOnlyList<EmergencyContact> contacts = await _contacts.GetActiveLinkedByLinkedUserIdAsync(monitorUserId, cancellationToken);
        if (contacts.Count == 0) throw new NotFoundAppException("No linked emergency contacts were found.");
        return contacts.Select(c => c.Id).ToArray();
    }

    private async Task<AlertAcknowledgement> EnsureAcknowledgementAsync(string monitorUserId, NotificationDeliveryAttempt attempt, CancellationToken cancellationToken)
    {
        DateTimeOffset now = _clock.UtcNow;
        var ack = new AlertAcknowledgement { UserId = attempt.UserId, MonitorUserId = monitorUserId, EmergencyContactId = attempt.EmergencyContactId, AlertDispatchId = attempt.AlertDispatchId, NotificationDeliveryAttemptId = attempt.Id, IncidentId = attempt.IncidentId, TripId = attempt.TripId, Status = AlertAcknowledgementStatus.Pending, ResponseType = AlertAcknowledgementResponseType.None, CreatedAtUtc = now, UpdatedAtUtc = now, IdempotencyKey = _keys.Create(monitorUserId, attempt.Id) };
        (AlertAcknowledgement persisted, _) = await _acknowledgements.AddOrGetDuplicateAsync(ack, cancellationToken);
        return persisted;
    }

    private async Task<User> GetUserAsync(string userId, UserRole requiredRole, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive) throw new UnauthorizedAppException("Invalid authentication credentials.");
        if (user.Role != requiredRole) throw new ForbiddenAppException("Alert acknowledgements are not available for this role.");
        return user;
    }

    private static AlertAcknowledgementResponse ToResponse(AlertAcknowledgement ack) => new(ack.Id, ack.AlertDispatchId, ack.NotificationDeliveryAttemptId, ack.IncidentId, ack.TripId, ack.EmergencyContactId, ack.Status.ToString(), ack.ResponseType.ToString(), ack.Message, ack.ViewedAtUtc, ack.AcknowledgedAtUtc, ack.DeclinedAtUtc, ack.CreatedAtUtc, ack.UpdatedAtUtc);
    private static TEnum? ParseEnum<TEnum>(string? value) where TEnum : struct => Enum.TryParse(value, ignoreCase: true, out TEnum result) ? result : null;
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
