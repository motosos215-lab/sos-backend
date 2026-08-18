using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MotoSOS.API.Modules.Auth.Sessions.Domain;

public sealed class SessionTakeoverToken
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public UserSessionType SessionType { get; set; } = UserSessionType.Unknown;
    public string NewClientDeviceId { get; set; } = string.Empty;
    public string NewDeviceName { get; set; } = string.Empty;
    public string NewPlatform { get; set; } = string.Empty;
    public string? NewOsVersion { get; set; }
    public string? NewAppVersion { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? UsedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string ActiveSessionId { get; set; } = string.Empty;
    public bool HasActiveTrip { get; set; }
    public string? ActiveTripId { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
}
