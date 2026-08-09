using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MotoSOS.API.Modules.Trips.Domain;

public sealed class Trip
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string UserId { get; set; } = string.Empty;
    public string VehicleId { get; set; } = string.Empty;
    public string MobileDeviceId { get; set; } = string.Empty;
    public string? SmartwatchDeviceId { get; set; }
    public TripStatus Status { get; set; } = TripStatus.Active;
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? FinishedAtUtc { get; set; }
    public DateTimeOffset? ClientStartedAtUtc { get; set; }
    public DateTimeOffset? ClientFinishedAtUtc { get; set; }
    public TripLocation? StartLocation { get; set; }
    public TripLocation? EndLocation { get; set; }
    public int? StartBatteryLevel { get; set; }
    public int? EndBatteryLevel { get; set; }
    public string? AppVersion { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
