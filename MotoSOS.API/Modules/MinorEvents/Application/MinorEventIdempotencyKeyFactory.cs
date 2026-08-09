using System.Security.Cryptography;
using System.Text;

namespace MotoSOS.API.Modules.MinorEvents.Application;

public sealed class MinorEventIdempotencyKeyFactory : IMinorEventIdempotencyKeyFactory
{
    public string Create(string userId, string tripId, string clientEventId, string eventType)
    {
        string raw = string.Join('|', userId.Trim(), tripId.Trim(), clientEventId.Trim(), eventType.Trim());
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
