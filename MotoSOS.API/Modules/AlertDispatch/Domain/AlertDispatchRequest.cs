using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MotoSOS.API.Modules.AlertDispatch.Domain;

public sealed class AlertDispatchRequest
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public string UserId { get; set; } = string.Empty;
    public string IncidentId { get; set; } = string.Empty;
    public string TripId { get; set; } = string.Empty;
    public string VehicleId { get; set; } = string.Empty;
    public string MobileDeviceId { get; set; } = string.Empty;
    public string? SmartwatchDeviceId { get; set; }
    public string ClientAlertRequestId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public AlertDispatchPriority Priority { get; set; }
    public AlertDispatchReason Reason { get; set; }
    public AlertDispatchStatus Status { get; set; } = AlertDispatchStatus.PendingDispatch;
    public DateTimeOffset RequestedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public DateTimeOffset? CancelledAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public string? Notes { get; set; }
    public IReadOnlyList<AlertContactSnapshot> ContactsSnapshot { get; set; } = [];
}
