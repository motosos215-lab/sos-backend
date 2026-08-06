using System.Security.Cryptography;
using System.Text;

namespace MotoSOS.API.Modules.OfflineIngestion.Application;

public sealed class OfflineIngestionIdempotencyKeyFactory : IOfflineIngestionIdempotencyKeyFactory
{
    public string Create(string userId, string mobileDeviceId, string tripId, string type, string clientEventId, int payloadVersion)
    {
        string material = string.Join('|', userId.Trim(), mobileDeviceId.Trim(), tripId.Trim(), type.Trim().ToLowerInvariant(), clientEventId.Trim(), payloadVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
