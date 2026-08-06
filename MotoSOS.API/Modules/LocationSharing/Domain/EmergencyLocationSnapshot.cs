using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MotoSOS.API.Modules.LocationSharing.Domain;

public sealed class EmergencyLocationSnapshot
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public string UserId { get; set; } = string.Empty;
    public string IncidentId { get; set; } = string.Empty;
    public string TripId { get; set; } = string.Empty;
    public string? AlertDispatchId { get; set; }
    public string MobileDeviceId { get; set; } = string.Empty;
    public string? SmartwatchDeviceId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? AccuracyMeters { get; set; }
    public double? AltitudeMeters { get; set; }
    public double? SpeedMetersPerSecond { get; set; }
    public double? HeadingDegrees { get; set; }
    public int? BatteryPercentage { get; set; }
    public LocationSharingSource Source { get; set; }
    public string ClientLocationUpdateId { get; set; } = string.Empty;
    public DateTimeOffset RecordedAtUtc { get; set; }
    public DateTimeOffset ReceivedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? DeactivatedAtUtc { get; set; }
    public LocationSharingDeactivationReason? DeactivationReason { get; set; }
}
