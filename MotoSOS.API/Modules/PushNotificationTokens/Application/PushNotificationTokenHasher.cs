using System.Security.Cryptography;
using System.Text;

namespace MotoSOS.API.Modules.PushNotificationTokens.Application;

public sealed class PushNotificationTokenHasher : IPushNotificationTokenHasher
{
    public string Hash(string token)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
