using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MotoSOS.API.Modules.NotificationPreferences.Domain;

public sealed class NotificationPreference
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string UserId { get; set; } = string.Empty;
    public bool PushEnabled { get; set; } = true;
    public bool EmailEnabled { get; set; }
    public bool SmsEnabled { get; set; }
    public bool CriticalAlertsEnabled { get; set; } = true;
    public bool TripUpdatesEnabled { get; set; } = true;
    public bool SecurityAlertsEnabled { get; set; } = true;
    public bool MarketingEnabled { get; set; }
    public bool QuietHoursEnabled { get; set; }
    public string? QuietHoursStartLocal { get; set; }
    public string? QuietHoursEndLocal { get; set; }
    public string TimeZone { get; set; } = "America/Mexico_City";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
