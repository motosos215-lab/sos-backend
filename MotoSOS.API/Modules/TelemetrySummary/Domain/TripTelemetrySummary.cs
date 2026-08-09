using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MotoSOS.API.Modules.Trips.Domain;

namespace MotoSOS.API.Modules.TelemetrySummary.Domain;

public sealed class TripTelemetrySummary
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public string UserId { get; set; } = string.Empty;
    public string TripId { get; set; } = string.Empty;
    public string VehicleId { get; set; } = string.Empty;
    public TripStatus TripStatus { get; set; }
    public TelemetrySummaryStatus SummaryStatus { get; set; } = TelemetrySummaryStatus.NoData;
    public int TotalMinorEvents { get; set; }
    public IReadOnlyDictionary<string, int> EventsByType { get; set; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> EventsBySeverity { get; set; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> EventsBySource { get; set; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> EventsByStatus { get; set; } = new Dictionary<string, int>();
    public int HardBrakeCount { get; set; }
    public int HarshAccelerationCount { get; set; }
    public int SharpTurnCount { get; set; }
    public int GpsSignalLostCount { get; set; }
    public int GpsSignalRecoveredCount { get; set; }
    public int LowBatteryCount { get; set; }
    public int SmartwatchDisconnectedCount { get; set; }
    public int SmartwatchReconnectedCount { get; set; }
    public int SensorAnomalyCount { get; set; }
    public int PossibleFallLowConfidenceCount { get; set; }
    public int InformationalCount { get; set; }
    public DateTimeOffset? FirstEventAtUtc { get; set; }
    public DateTimeOffset? LastEventAtUtc { get; set; }
    public double? AverageConfidence { get; set; }
    public double? MaxScore { get; set; }
    public double? AverageScore { get; set; }
    public int? MinBatteryLevel { get; set; }
    public double? MaxSpeedKmh { get; set; }
    public IReadOnlyDictionary<string, int> GpsQualitySamples { get; set; } = new Dictionary<string, int>();
    public DateTimeOffset LastComputedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
