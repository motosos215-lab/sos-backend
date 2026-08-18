using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MotoSOS.API.Modules.Auth.Sessions.Domain;

public sealed class UserSession
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public UserSessionType SessionType { get; set; } = UserSessionType.Unknown;
    public string ClientDeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string? OsVersion { get; set; }
    public string? AppVersion { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset LastSeenAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public string? RevokedReason { get; set; }
    public string? ReplacedBySessionId { get; set; }
    public string? TakeoverSource { get; set; }
    public string? IpAddressHash { get; set; }
    public string? UserAgentHash { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
