using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MotoSOS.API.Modules.Escalations.Domain;

public sealed class EmergencyEscalation
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public string UserId { get; set; } = string.Empty;
    public string IncidentId { get; set; } = string.Empty;
    public string TripId { get; set; } = string.Empty;
    public string AlertDispatchId { get; set; } = string.Empty;
    public EmergencyEscalationStatus Status { get; set; } = EmergencyEscalationStatus.Requested;
    public EmergencyEscalationReason Reason { get; set; }
    public EmergencyEscalationLevel Level { get; set; }
    public string RequestedByUserId { get; set; } = string.Empty;
    public string RequestedByRole { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public int NotificationsTotal { get; set; }
    public int NotificationsPrepared { get; set; }
    public int NotificationsSimulatedSent { get; set; }
    public int NotificationsFailed { get; set; }
    public int NotificationsCancelled { get; set; }
    public int AcknowledgementsTotal { get; set; }
    public int AcknowledgedCount { get; set; }
    public int DeclinedCount { get; set; }
    public int ViewedCount { get; set; }
    public int PendingCount { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public DateTimeOffset? ResolvedAtUtc { get; set; }
    public DateTimeOffset? MarkedUnresolvedAtUtc { get; set; }
    public DateTimeOffset? CancelledAtUtc { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
}
