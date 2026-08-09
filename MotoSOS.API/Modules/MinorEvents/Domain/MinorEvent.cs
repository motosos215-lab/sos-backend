using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MotoSOS.API.Modules.MinorEvents.Domain;

public sealed class MinorEvent
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public string UserId { get; set; } = string.Empty;
    public string TripId { get; set; } = string.Empty;
    public string VehicleId { get; set; } = string.Empty;
    public string? MobileDeviceId { get; set; }
    public string? SmartwatchDeviceId { get; set; }
    public string ClientEventId { get; set; } = string.Empty;
    public MinorEventType EventType { get; set; } = MinorEventType.Unknown;
    public MinorEventSeverity Severity { get; set; } = MinorEventSeverity.Info;
    public MinorEventSource Source { get; set; } = MinorEventSource.Unknown;
    public MinorEventStatus Status { get; set; } = MinorEventStatus.Recorded;
    public double? Score { get; set; }
    public double Confidence { get; set; }
    public string? GpsQuality { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? SpeedKmh { get; set; }
    public int? BatteryLevel { get; set; }
    public string? Message { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public DateTimeOffset ReceivedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public string? ProcessedFromOfflineIngestionRecordId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public IReadOnlyDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}
