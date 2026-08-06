using System.Security.Cryptography;
using System.Text;
using MotoSOS.API.Modules.Notifications.Domain;

namespace MotoSOS.API.Modules.Notifications.Application;

public sealed class NotificationIdempotencyKeyFactory : INotificationIdempotencyKeyFactory
{
    public string Create(string userId, string alertDispatchId, string emergencyContactId, NotificationChannel channel, int attemptNumber)
    {
        string raw = string.Join('|', userId.Trim(), alertDispatchId.Trim(), emergencyContactId.Trim(), channel.ToString(), attemptNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }
}
