using System.Text.Json.Serialization;

namespace MotoSOS.API.Modules.PushNotificationTokens.Contracts;

public sealed record RegisterPushNotificationTokenRequest(
    string? Platform,
    string? Channel,
    string? DeviceId,
    string? Token,
    IReadOnlyDictionary<string, string>? Metadata)
{
    [JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? ExtraProperties { get; init; }
}
