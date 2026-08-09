using System.Security.Cryptography;
using System.Text;

namespace MotoSOS.API.Modules.Escalations.Application;

public sealed class EmergencyEscalationIdempotencyKeyFactory : IEmergencyEscalationIdempotencyKeyFactory
{
    public string Create(string userId, string alertDispatchId)
    {
        string input = $"{userId.Trim()}|{alertDispatchId.Trim()}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
