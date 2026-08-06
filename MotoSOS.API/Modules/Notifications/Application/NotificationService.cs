using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AlertDispatch.Application;
using MotoSOS.API.Modules.AlertDispatch.Domain;
using MotoSOS.API.Modules.Notifications.Contracts;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.Onboarding.Application;
using MotoSOS.API.Modules.Onboarding.Contracts;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Modules.Notifications.Application;

public sealed class NotificationService : INotificationService
{
    private const int DefaultPageNumber = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;
    private const int AttemptNumber = 1;

    private readonly IUserRepository _users;
    private readonly IOnboardingService _onboarding;
    private readonly IAlertDispatchRepository _alertDispatches;
    private readonly INotificationDeliveryAttemptRepository _attempts;
    private readonly INotificationIdempotencyKeyFactory _idempotencyKeys;
    private readonly IClock _clock;

    public NotificationService(IUserRepository users, IOnboardingService onboarding, IAlertDispatchRepository alertDispatches, INotificationDeliveryAttemptRepository attempts, INotificationIdempotencyKeyFactory idempotencyKeys, IClock clock)
    {
        _users = users;
        _onboarding = onboarding;
        _alertDispatches = alertDispatches;
        _attempts = attempts;
        _idempotencyKeys = idempotencyKeys;
        _clock = clock;
    }

    public async Task<PrepareNotificationAttemptsResponse> PrepareAsync(string userId, PrepareNotificationAttemptsRequest request, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        await EnsureOnboardingReadyAsync(user.Id, cancellationToken);
        AlertDispatchRequest alertDispatch = await GetOwnedAlertDispatchAsync(user.Id, NormalizeRequired(request.AlertDispatchId), cancellationToken);
        EnsureAlertDispatchReady(alertDispatch);
        if (alertDispatch.ContactsSnapshot.Count == 0) throw new NotificationNotAllowedAppException("Alert dispatch has no contacts snapshot.");

        DateTimeOffset now = _clock.UtcNow;
        List<NotificationDeliveryAttempt> persisted = [];
        foreach (AlertContactSnapshot contact in alertDispatch.ContactsSnapshot)
        {
            NotificationChannel? channel = SelectChannel(contact);
            if (!channel.HasValue) continue;
            var attempt = new NotificationDeliveryAttempt
            {
                UserId = user.Id,
                AlertDispatchId = alertDispatch.Id,
                IncidentId = alertDispatch.IncidentId,
                TripId = alertDispatch.TripId,
                EmergencyContactId = contact.EmergencyContactId,
                ContactFullName = contact.FullName,
                ContactPhoneNumber = contact.PhoneNumber,
                ContactEmail = contact.Email,
                ContactRelationship = contact.Relationship,
                ContactPriority = contact.Priority,
                Channel = channel.Value,
                Status = NotificationDeliveryStatus.Prepared,
                Provider = NotificationProvider.None,
                AttemptNumber = AttemptNumber,
                IdempotencyKey = _idempotencyKeys.Create(user.Id, alertDispatch.Id, contact.EmergencyContactId, channel.Value, AttemptNumber),
                PreparedAtUtc = now,
                LastStatusChangedAtUtc = now,
                Notes = NormalizeOptional(request.Notes),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            (NotificationDeliveryAttempt saved, _) = await _attempts.AddOrGetDuplicateAsync(attempt, cancellationToken);
            persisted.Add(saved);
        }

        if (persisted.Count == 0) throw new NotificationNotAllowedAppException("Alert dispatch contacts do not have notification channels available.");
        return new PrepareNotificationAttemptsResponse(persisted.Select(ToResponse).ToArray());
    }

    public async Task<GetNotificationDeliveryAttemptsResponse> ListAsync(string userId, string? alertDispatchId, string? incidentId, string? status, int? pageNumber, int? pageSize, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        NotificationDeliveryStatus? parsedStatus = ParseEnum<NotificationDeliveryStatus>(status);
        int normalizedPageNumber = Math.Max(pageNumber ?? DefaultPageNumber, 1);
        int normalizedPageSize = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        IReadOnlyList<NotificationDeliveryAttempt> attempts = await _attempts.ListByUserIdAsync(user.Id, NormalizeOptional(alertDispatchId), NormalizeOptional(incidentId), parsedStatus, normalizedPageNumber, normalizedPageSize, cancellationToken);
        long total = await _attempts.CountByUserIdAsync(user.Id, NormalizeOptional(alertDispatchId), NormalizeOptional(incidentId), parsedStatus, cancellationToken);
        return new GetNotificationDeliveryAttemptsResponse(attempts.Select(ToResponse).ToArray(), normalizedPageNumber, normalizedPageSize, total);
    }

    public async Task<GetNotificationDeliveryAttemptResponse> GetAsync(string userId, string id, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        return new GetNotificationDeliveryAttemptResponse(ToResponse(await GetOwnedAttemptAsync(user.Id, id, cancellationToken)));
    }

    public async Task<MarkNotificationSimulatedSentResponse> MarkSimulatedSentAsync(string userId, string id, MarkNotificationSimulatedSentRequest request, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        NotificationDeliveryAttempt attempt = await GetOwnedAttemptAsync(user.Id, id, cancellationToken);
        if (attempt.Status == NotificationDeliveryStatus.SimulatedSent) return new MarkNotificationSimulatedSentResponse(ToResponse(attempt));
        if (attempt.Status != NotificationDeliveryStatus.Prepared) throw new NotificationNotAllowedAppException("Only prepared attempts can be marked as simulated sent.");
        DateTimeOffset now = _clock.UtcNow;
        attempt.Status = NotificationDeliveryStatus.SimulatedSent;
        attempt.Provider = NotificationProvider.Simulated;
        attempt.ProviderMessageId = NormalizeOptional(request.ProviderMessageId);
        attempt.SimulatedSentAtUtc = now;
        attempt.LastStatusChangedAtUtc = now;
        attempt.UpdatedAtUtc = now;
        attempt.Notes = NormalizeOptional(request.Notes) ?? attempt.Notes;
        await _attempts.UpdateAsync(attempt, cancellationToken);
        return new MarkNotificationSimulatedSentResponse(ToResponse(attempt));
    }

    public async Task<MarkNotificationFailedResponse> MarkFailedAsync(string userId, string id, MarkNotificationFailedRequest request, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        NotificationDeliveryAttempt attempt = await GetOwnedAttemptAsync(user.Id, id, cancellationToken);
        if (attempt.Status == NotificationDeliveryStatus.Failed) return new MarkNotificationFailedResponse(ToResponse(attempt));
        if (attempt.Status != NotificationDeliveryStatus.Prepared) throw new NotificationNotAllowedAppException("Only prepared attempts can be marked as failed.");
        DateTimeOffset now = _clock.UtcNow;
        attempt.Status = NotificationDeliveryStatus.Failed;
        attempt.FailedAtUtc = now;
        attempt.LastStatusChangedAtUtc = now;
        attempt.FailureReason = NormalizeRequired(request.FailureReason);
        attempt.UpdatedAtUtc = now;
        attempt.Notes = NormalizeOptional(request.Notes) ?? attempt.Notes;
        await _attempts.UpdateAsync(attempt, cancellationToken);
        return new MarkNotificationFailedResponse(ToResponse(attempt));
    }

    public async Task<CancelNotificationAttemptResponse> CancelAsync(string userId, string id, CancelNotificationAttemptRequest request, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        NotificationDeliveryAttempt attempt = await GetOwnedAttemptAsync(user.Id, id, cancellationToken);
        if (attempt.Status == NotificationDeliveryStatus.Cancelled) return new CancelNotificationAttemptResponse(ToResponse(attempt));
        if (attempt.Status != NotificationDeliveryStatus.Prepared) throw new NotificationNotAllowedAppException("Only prepared attempts can be cancelled.");
        DateTimeOffset now = _clock.UtcNow;
        attempt.Status = NotificationDeliveryStatus.Cancelled;
        attempt.CancelledAtUtc = request.ClientCancelledAtUtc ?? now;
        attempt.LastStatusChangedAtUtc = now;
        attempt.UpdatedAtUtc = now;
        attempt.Notes = NormalizeOptional(request.Reason) ?? attempt.Notes;
        await _attempts.UpdateAsync(attempt, cancellationToken);
        return new CancelNotificationAttemptResponse(ToResponse(attempt));
    }

    private async Task<User> GetRiderUserAsync(string userId, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive) throw new UnauthorizedAppException("Invalid authentication credentials.");
        if (user.Role != UserRole.Rider) throw new ForbiddenAppException("Notifications API is available only for riders.");
        return user;
    }

    private async Task EnsureOnboardingReadyAsync(string userId, CancellationToken cancellationToken)
    {
        OnboardingStatusResponse status = await _onboarding.GetStatusAsync(userId, cancellationToken);
        if (status.CompletedSteps != 7 || status.CurrentStep != "Completed" || !status.IsOperational) throw new OnboardingNotReadyAppException("Onboarding must be completed before preparing notifications.");
    }

    private async Task<AlertDispatchRequest> GetOwnedAlertDispatchAsync(string userId, string id, CancellationToken cancellationToken)
    {
        AlertDispatchRequest? alertDispatch = await _alertDispatches.GetByIdAsync(id, cancellationToken);
        if (alertDispatch is null || alertDispatch.UserId != userId) throw new NotFoundAppException("Alert dispatch was not found.");
        return alertDispatch;
    }

    private async Task<NotificationDeliveryAttempt> GetOwnedAttemptAsync(string userId, string id, CancellationToken cancellationToken)
    {
        NotificationDeliveryAttempt? attempt = await _attempts.GetByIdAsync(id, cancellationToken);
        if (attempt is null || attempt.UserId != userId) throw new NotFoundAppException("Notification delivery attempt was not found.");
        return attempt;
    }

    private static void EnsureAlertDispatchReady(AlertDispatchRequest alertDispatch)
    {
        if (alertDispatch.Status == AlertDispatchStatus.Completed) throw new AlertDispatchAlreadyCompletedAppException("Alert dispatch is already completed.");
        if (alertDispatch.Status != AlertDispatchStatus.PendingDispatch) throw new AlertDispatchNotReadyAppException("Alert dispatch is not ready for notification attempts.");
    }

    private static NotificationChannel? SelectChannel(AlertContactSnapshot contact)
    {
        if (!string.IsNullOrWhiteSpace(contact.PhoneNumber)) return NotificationChannel.Sms;
        if (!string.IsNullOrWhiteSpace(contact.Email)) return NotificationChannel.Email;
        return null;
    }

    private static NotificationDeliveryAttemptResponse ToResponse(NotificationDeliveryAttempt attempt) => new(attempt.Id, attempt.AlertDispatchId, attempt.IncidentId, attempt.TripId, attempt.EmergencyContactId, attempt.ContactFullName, attempt.ContactRelationship, attempt.ContactPriority, attempt.Channel.ToString(), attempt.Status.ToString(), attempt.Provider.ToString(), attempt.AttemptNumber, attempt.PreparedAtUtc, attempt.SimulatedSentAtUtc, attempt.FailedAtUtc, attempt.CancelledAtUtc, attempt.LastStatusChangedAtUtc, attempt.FailureReason, attempt.Notes, attempt.CreatedAtUtc, attempt.UpdatedAtUtc);
    private static TEnum? ParseEnum<TEnum>(string? value) where TEnum : struct => Enum.TryParse(value, ignoreCase: true, out TEnum result) ? result : null;
    private static string NormalizeRequired(string? value) => value?.Trim() ?? string.Empty;
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
