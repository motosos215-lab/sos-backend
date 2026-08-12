namespace MotoSOS.API.Modules.NotificationPreferences.Contracts;

public sealed record NotificationPreferenceResponse(
    bool PushEnabled,
    bool EmailEnabled,
    bool SmsEnabled,
    bool CriticalAlertsEnabled,
    bool TripUpdatesEnabled,
    bool SecurityAlertsEnabled,
    bool MarketingEnabled,
    bool QuietHoursEnabled,
    string? QuietHoursStartLocal,
    string? QuietHoursEndLocal,
    string TimeZone,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);
