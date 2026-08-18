using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Modules.AlertAcknowledgements.Domain;
using MotoSOS.API.Modules.NotificationPreferences.Application;
using MotoSOS.API.Modules.NotificationPreferences.Domain;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.PushNotificationTokens.Application;

namespace MotoSOS.API.Modules.Notifications.Application;

public sealed class RiderAlertFeedbackNotificationService : IRiderAlertFeedbackNotificationService
{
    private const int AttemptNumber = 1;

    private readonly INotificationDeliveryAttemptRepository _attempts;
    private readonly INotificationPreferenceRepository _preferences;
    private readonly IPushNotificationTokenRepository _pushTokens;
    private readonly IRiderAlertFeedbackNotificationIdempotencyKeyFactory _keys;
    private readonly IClock _clock;

    public RiderAlertFeedbackNotificationService(INotificationDeliveryAttemptRepository attempts, INotificationPreferenceRepository preferences, IPushNotificationTokenRepository pushTokens, IRiderAlertFeedbackNotificationIdempotencyKeyFactory keys, IClock clock)
    {
        _attempts = attempts;
        _preferences = preferences;
        _pushTokens = pushTokens;
        _keys = keys;
        _clock = clock;
    }

    public async Task EnqueueAsync(AlertAcknowledgement acknowledgement, string feedbackEventType, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(acknowledgement.UserId) || string.IsNullOrWhiteSpace(acknowledgement.IncidentId) || string.IsNullOrWhiteSpace(acknowledgement.NotificationDeliveryAttemptId)) return;

            NotificationPreference? preference = await _preferences.GetByUserIdAsync(acknowledgement.UserId, cancellationToken);
            if (preference is not null && !preference.PushEnabled) return;
            if (await _pushTokens.GetLatestActiveFcmByUserIdAsync(acknowledgement.UserId, cancellationToken) is null) return;

            DateTimeOffset now = _clock.UtcNow;
            var feedbackAttempt = new NotificationDeliveryAttempt
            {
                UserId = acknowledgement.UserId,
                AlertDispatchId = acknowledgement.AlertDispatchId,
                IncidentId = acknowledgement.IncidentId,
                TripId = acknowledgement.TripId,
                EmergencyContactId = acknowledgement.EmergencyContactId,
                RecipientUserId = acknowledgement.UserId,
                EventType = feedbackEventType,
                MonitorAlertAttemptId = acknowledgement.NotificationDeliveryAttemptId,
                MonitorUserId = acknowledgement.MonitorUserId,
                Screen = NotificationScreens.EmergencyStatus,
                OccurredAtUtc = occurredAtUtc,
                Channel = NotificationChannel.Push,
                Status = NotificationDeliveryStatus.Prepared,
                Provider = NotificationProvider.None,
                AttemptNumber = AttemptNumber,
                IdempotencyKey = _keys.Create(acknowledgement.UserId, acknowledgement.IncidentId, acknowledgement.NotificationDeliveryAttemptId, feedbackEventType),
                PreparedAtUtc = now,
                LastStatusChangedAtUtc = now,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            await _attempts.AddOrGetDuplicateAsync(feedbackAttempt, cancellationToken);
        }
        catch
        {
        }
    }
}
