using System.Security.Cryptography;
using System.Text;

namespace MotoSOS.API.Modules.EmergencyResolution.Application;

public sealed class EmergencyResolutionIdempotencyKeyFactory : IEmergencyResolutionIdempotencyKeyFactory
{
    public string Create(string userId, string incidentId)
    {
        string raw = $"emergency-resolution:{userId.Trim()}:{incidentId.Trim()}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
