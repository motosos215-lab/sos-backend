using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Modules.AuditLogRetention.Domain;

public sealed class AuditLogRetentionRun
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public string RequestedByUserId { get; set; } = string.Empty;
    public UserRole RequestedByRole { get; set; }
    public AuditLogRetentionMode Mode { get; set; } = AuditLogRetentionMode.DryRun;
    public AuditLogRetentionRunStatus Status { get; set; } = AuditLogRetentionRunStatus.Completed;
    public int RetentionDays { get; set; }
    public DateTimeOffset CutoffUtc { get; set; }
    public long CandidateCount { get; set; }
    public long DeletedCount { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}
