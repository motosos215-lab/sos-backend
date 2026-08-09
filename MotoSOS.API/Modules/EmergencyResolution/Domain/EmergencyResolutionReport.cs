using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MotoSOS.API.Modules.EmergencyResolution.Domain;

public sealed class EmergencyResolutionReport
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public string UserId { get; set; } = string.Empty;
    public string IncidentId { get; set; } = string.Empty;
    public string TripId { get; set; } = string.Empty;
    public string? AlertDispatchId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public EmergencyResolutionOutcome Outcome { get; set; } = EmergencyResolutionOutcome.Unknown;
    public EmergencyClosedByRole ClosedByRole { get; set; } = EmergencyClosedByRole.Unknown;
    public string Summary { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTimeOffset IncidentCreatedAtUtc { get; set; }
    public DateTimeOffset IncidentClosedAtUtc { get; set; }
    public int NotificationAttemptsTotal { get; set; }
    public int AcknowledgementsTotal { get; set; }
    public int AcknowledgedCount { get; set; }
    public int DeclinedCount { get; set; }
    public DateTimeOffset? FirstNotificationPreparedAtUtc { get; set; }
    public DateTimeOffset? FirstAcknowledgedAtUtc { get; set; }
    public long? ResponseTimeSeconds { get; set; }
    public double? FinalLatitude { get; set; }
    public double? FinalLongitude { get; set; }
    public DateTimeOffset? FinalLocationRecordedAtUtc { get; set; }
    public bool? LastKnownLocationWasStale { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
