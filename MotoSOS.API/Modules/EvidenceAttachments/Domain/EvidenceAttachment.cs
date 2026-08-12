using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Modules.EvidenceAttachments.Domain;

public sealed class EvidenceAttachment
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public string UserId { get; set; } = string.Empty;
    public string RegisteredByUserId { get; set; } = string.Empty;
    public UserRole RegisteredByRole { get; set; }
    public EvidenceTargetType TargetType { get; set; }
    public string? IncidentId { get; set; }
    public string? AlertDispatchId { get; set; }
    public string? EmergencyResolutionReportId { get; set; }
    public string? TripId { get; set; }
    public EvidenceType EvidenceType { get; set; }
    public EvidenceSource Source { get; set; }
    public EvidenceAttachmentStatus Status { get; set; } = EvidenceAttachmentStatus.Registered;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? Sha256Hash { get; set; }
    public string ClientEvidenceId { get; set; } = string.Empty;
    public string? ClientStorageReference { get; set; }
    public EvidenceStorageProvider StorageProvider { get; set; } = EvidenceStorageProvider.None;
    public string? Bucket { get; set; }
    public string? StorageObjectKey { get; set; }
    public string? OriginalFileName { get; set; }
    public string? StoredFileName { get; set; }
    public DateTimeOffset? UploadedAtUtc { get; set; }
    public string? UploadedByUserId { get; set; }
    public UserRole? UploadedByRole { get; set; }
    public bool IsDeleted { get; set; }
    public long DownloadCount { get; set; }
    public DateTimeOffset? LastDownloadedAtUtc { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset CapturedAtUtc { get; set; }
    public DateTimeOffset RegisteredAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = [];
}
