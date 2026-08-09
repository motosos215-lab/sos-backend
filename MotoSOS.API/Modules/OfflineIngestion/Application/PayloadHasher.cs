using System.Security.Cryptography;
using System.Text;

namespace MotoSOS.API.Modules.OfflineIngestion.Application;

public sealed class PayloadHasher : IPayloadHasher
{
    public string Hash(string payloadJson)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
