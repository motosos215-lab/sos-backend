using System.Security.Cryptography;

namespace MotoSOS.API.Modules.EmergencyContacts.Application;

public sealed class LinkingCodeGenerator : ILinkingCodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public string CreateCode()
    {
        return $"{Segment()}-{Segment()}-{Segment()}";
    }

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
