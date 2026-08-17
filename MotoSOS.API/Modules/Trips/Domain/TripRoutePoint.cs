using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MotoSOS.API.Modules.Trips.Domain;

public sealed class TripRoutePoint
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string TripId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string ClientRoutePointId { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double AccuracyMeters { get; set; }
    public double? SpeedMetersPerSecond { get; set; }
    public double? BearingDegrees { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public TripRoutePointSource Source { get; set; } = TripRoutePointSource.Android;
    public string? AppVersion { get; set; }
    public string? DeviceId { get; set; }
}
