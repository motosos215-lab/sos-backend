using System.Security.Cryptography;

namespace MotoSOS.API.Modules.Devices.Application;

public sealed class ActivationCodeGenerator : IActivationCodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public string CreateCode() => $"MSOS-{Segment()}-{Segment()}";

    private static string Segment()
    {
        Span<char> chars = stackalloc char[4];
        for (int index = 0; index < chars.Length; index++)
        {
            chars[index] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(chars);
    }
}
