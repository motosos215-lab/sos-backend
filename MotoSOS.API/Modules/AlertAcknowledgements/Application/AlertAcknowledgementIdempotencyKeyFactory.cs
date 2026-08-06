using System.Security.Cryptography;
using System.Text;

namespace MotoSOS.API.Modules.AlertAcknowledgements.Application;

public sealed class AlertAcknowledgementIdempotencyKeyFactory : IAlertAcknowledgementIdempotencyKeyFactory
{
    public string Create(string monitorUserId, string notificationDeliveryAttemptId)
    {
        string raw = string.Join('|', monitorUserId.Trim(), notificationDeliveryAttemptId.Trim());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }
}
