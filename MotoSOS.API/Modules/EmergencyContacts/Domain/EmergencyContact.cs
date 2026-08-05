using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MotoSOS.API.Modules.EmergencyContacts.Domain;

public sealed class EmergencyContact
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string UserId { get; set; } = string.Empty;

    public string? FullName { get; set; }

    public string? Relationship { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Email { get; set; }

    public int? Priority { get; set; }

    public EmergencyContactInvitationStatus InvitationStatus { get; set; } = EmergencyContactInvitationStatus.Draft;

    public string? LinkingCode { get; set; }

    public DateTimeOffset? LinkingCodeExpiresAtUtc { get; set; }

    public string? LinkedUserId { get; set; }

    public EmergencyContactPermissions Permissions { get; set; } = new();

    public bool IsPrimary { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public DateTimeOffset? InvitedAtUtc { get; set; }

    public DateTimeOffset? LinkedAtUtc { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }
}
