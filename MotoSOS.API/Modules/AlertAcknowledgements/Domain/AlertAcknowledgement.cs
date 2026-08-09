using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MotoSOS.API.Modules.AlertAcknowledgements.Domain;

public sealed class AlertAcknowledgement
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public string UserId { get; set; } = string.Empty;
    public string MonitorUserId { get; set; } = string.Empty;
    public string EmergencyContactId { get; set; } = string.Empty;
    public string AlertDispatchId { get; set; } = string.Empty;
    public string NotificationDeliveryAttemptId { get; set; } = string.Empty;
    public string IncidentId { get; set; } = string.Empty;
    public string TripId { get; set; } = string.Empty;
    public AlertAcknowledgementStatus Status { get; set; } = AlertAcknowledgementStatus.Pending;
    public AlertAcknowledgementResponseType ResponseType { get; set; } = AlertAcknowledgementResponseType.None;
    public string? Message { get; set; }
    public DateTimeOffset? ViewedAtUtc { get; set; }
    public DateTimeOffset? AcknowledgedAtUtc { get; set; }
    public DateTimeOffset? DeclinedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
}
