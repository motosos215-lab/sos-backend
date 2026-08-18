using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace MotoSOS.API.Modules.Notifications.Application;

public sealed class RiderAlertFeedbackNotificationIdempotencyKeyFactory : IRiderAlertFeedbackNotificationIdempotencyKeyFactory
{
    public string Create(string riderUserId, string incidentId, string monitorNotificationDeliveryAttemptId, string feedbackEventType)
    {
        string raw = string.Join('|', riderUserId.Trim(), incidentId.Trim(), monitorNotificationDeliveryAttemptId.Trim(), feedbackEventType.Trim().ToLower(CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }
}
