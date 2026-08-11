using System.Security.Cryptography;

namespace MotoSOS.API.Modules.Auth.Application;

public sealed class AuthCodeGenerator : IAuthCodeGenerator
{
    public string CreateCode(int length)
    {
        length = Math.Clamp(length, 4, 12);
        char[] chars = new char[length];
        for (int index = 0; index < chars.Length; index++)
        {
            chars[index] = (char)('0' + RandomNumberGenerator.GetInt32(10));
        }

        return new string(chars);
    }
}
