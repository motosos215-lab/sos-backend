using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MotoSOS.API.Modules.Auth.Domain;

public sealed class AuthCode
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string EmailNormalized { get; set; } = string.Empty;

    public string? UserId { get; set; }

    public AuthCodePurpose Purpose { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public AuthCodeStatus Status { get; set; } = AuthCodeStatus.Active;
    public int Attempts { get; set; }
    public int MaxAttempts { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UsedAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public DateTimeOffset? LastAttemptAtUtc { get; set; }
    public AuthCodeDeliveryChannel DeliveryChannel { get; set; } = AuthCodeDeliveryChannel.Simulated;
    public AuthCodeDeliveryStatus DeliveryStatus { get; set; } = AuthCodeDeliveryStatus.Pending;
    public Dictionary<string, string> Metadata { get; set; } = [];
}
