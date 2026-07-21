using System.Security.Cryptography;
using System.Text;

namespace MotoSOS.API.Security.Tokens;

public sealed class RefreshTokenGenerator : IRefreshTokenGenerator
{
    public string CreateToken()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    public string HashToken(string plainValue)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainValue));
        return Convert.ToBase64String(bytes);
    }
}
