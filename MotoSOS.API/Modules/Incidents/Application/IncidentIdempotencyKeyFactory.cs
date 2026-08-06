using System.Security.Cryptography;
using System.Text;

namespace MotoSOS.API.Modules.Incidents.Application;

public sealed class IncidentIdempotencyKeyFactory : IIncidentIdempotencyKeyFactory
{
    public string Create(string userId, string tripId, string clientIncidentId)
    {
        string material = string.Join('|', userId.Trim(), tripId.Trim(), clientIncidentId.Trim());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
    }
}
