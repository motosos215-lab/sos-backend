using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MotoSOS.API.Modules.Incidents.Domain;

public sealed class Incident
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string UserId { get; set; } = string.Empty;
    public string TripId { get; set; } = string.Empty;
    public string VehicleId { get; set; } = string.Empty;
    public string MobileDeviceId { get; set; } = string.Empty;
    public string? SmartwatchDeviceId { get; set; }
    public string ClientIncidentId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public IncidentSource Source { get; set; }
    public IncidentCause Cause { get; set; }
    public IncidentRiskLevel RiskLevel { get; set; }
    public IncidentStatus Status { get; set; } = IncidentStatus.Open;
    public int? Score { get; set; }
    public double? Confidence { get; set; }
    public string? GpsQuality { get; set; }
    public string? RuleSetVersion { get; set; }
    public string? ValidationPolicyVersion { get; set; }
    public IncidentLocation? Location { get; set; }
    public IncidentEvidenceSummary? EvidenceSummary { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public DateTimeOffset? CancelledAtUtc { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public string? ClosureReason { get; set; }
    public string? ClosureNotes { get; set; }
    public string? ClosedByUserId { get; set; }
}
