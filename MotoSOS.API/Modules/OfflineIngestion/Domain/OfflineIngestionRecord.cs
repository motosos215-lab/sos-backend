using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MotoSOS.API.Modules.OfflineIngestion.Domain;

public sealed class OfflineIngestionRecord
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string UserId { get; set; } = string.Empty;
    public string MobileDeviceId { get; set; } = string.Empty;
    public string TripId { get; set; } = string.Empty;
    public string BatchId { get; set; } = string.Empty;
    public string ClientEventId { get; set; } = string.Empty;
    public OfflineIngestionItemType Type { get; set; }
    public int PayloadVersion { get; set; }
    public int SchemaVersion { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string AckId { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; set; }
    public DateTimeOffset ReceivedAtUtc { get; set; }
    public OfflineIngestionProcessingStatus ProcessingStatus { get; set; } = OfflineIngestionProcessingStatus.PendingProcessing;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
