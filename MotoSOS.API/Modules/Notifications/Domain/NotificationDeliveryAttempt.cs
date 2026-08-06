using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MotoSOS.API.Modules.Notifications.Domain;

public sealed class NotificationDeliveryAttempt
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public string UserId { get; set; } = string.Empty;
    public string AlertDispatchId { get; set; } = string.Empty;
    public string IncidentId { get; set; } = string.Empty;
    public string TripId { get; set; } = string.Empty;
    public string EmergencyContactId { get; set; } = string.Empty;
    public string? ContactFullName { get; set; }
    public string? ContactPhoneNumber { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactRelationship { get; set; }
    public int? ContactPriority { get; set; }
    public NotificationChannel Channel { get; set; }
    public NotificationDeliveryStatus Status { get; set; } = NotificationDeliveryStatus.Prepared;
    public NotificationProvider Provider { get; set; } = NotificationProvider.None;
    public string? ProviderMessageId { get; set; }
    public int AttemptNumber { get; set; } = 1;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTimeOffset PreparedAtUtc { get; set; }
    public DateTimeOffset? SimulatedSentAtUtc { get; set; }
    public DateTimeOffset? FailedAtUtc { get; set; }
    public DateTimeOffset? CancelledAtUtc { get; set; }
    public DateTimeOffset? LastStatusChangedAtUtc { get; set; }
    public string? FailureReason { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
