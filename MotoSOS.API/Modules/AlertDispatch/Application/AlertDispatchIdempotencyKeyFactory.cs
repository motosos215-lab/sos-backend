using System.Security.Cryptography;
using System.Text;

namespace MotoSOS.API.Modules.AlertDispatch.Application;

public sealed class AlertDispatchIdempotencyKeyFactory : IAlertDispatchIdempotencyKeyFactory
{
    public string Create(string userId, string incidentId, string clientAlertRequestId)
    {
        string raw = string.Join('|', userId.Trim(), incidentId.Trim(), clientAlertRequestId.Trim().ToLowerInvariant());
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
