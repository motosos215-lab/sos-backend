using System.Security.Cryptography;
using System.Text;
using MotoSOS.API.Modules.PushNotificationTokens.Domain;

namespace MotoSOS.API.Modules.PushNotificationTokens.Application;

public sealed class PushNotificationTokenIdempotencyKeyFactory : IPushNotificationTokenIdempotencyKeyFactory
{
    public string Create(string userId, string tokenHash, PushTokenPlatform platform, PushTokenChannel channel, string? deviceId)
    {
        string raw = string.Join('|', userId.Trim(), tokenHash.Trim(), platform.ToString(), channel.ToString(), Normalize(deviceId) ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
