using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MotoSOS.API.Modules.PushNotificationTokens.Domain;

public sealed class PushNotificationToken
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public string UserId { get; set; } = string.Empty;
    public string? SessionId { get; set; }
    public string? DeviceId { get; set; }
    public PushTokenPlatform Platform { get; set; }
    public PushTokenChannel Channel { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string TokenValue { get; set; } = string.Empty;
    public string TokenPreview { get; set; } = string.Empty;
    public PushNotificationTokenStatus Status { get; set; } = PushNotificationTokenStatus.Active;
    public DateTimeOffset RegisteredAtUtc { get; set; }
    public DateTimeOffset LastSeenAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = [];
}
