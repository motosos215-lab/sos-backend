using System.Text.Json.Serialization;

namespace MotoSOS.API.Modules.NotificationPreferences.Contracts;

public sealed record UpdateNotificationPreferenceRequest(
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
    string TimeZone)
{
    [JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? ExtraProperties { get; init; }
}
