using MotoSOS.API.Modules.EmergencyContacts.Application;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
using MotoSOS.API.Modules.Notifications.Application;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.PushNotificationTokens.Application;
using MotoSOS.API.Modules.PushNotificationTokens.Domain;

namespace MotoSOS.API.Modules.Notifications.Providers;

public sealed class PushNotificationRecipientResolver : IPushNotificationRecipientResolver
{
    private readonly INotificationDeliveryAttemptRepository _attempts;
    private readonly IEmergencyContactRepository _contacts;
    private readonly IPushNotificationTokenRepository _tokens;

    public PushNotificationRecipientResolver(INotificationDeliveryAttemptRepository attempts, IEmergencyContactRepository contacts, IPushNotificationTokenRepository tokens)
    {
        _attempts = attempts;
        _contacts = contacts;
        _tokens = tokens;
    }

    public async Task<PushNotificationRecipientResolution> ResolveAsync(string notificationDeliveryAttemptId, CancellationToken cancellationToken)
    {
        NotificationDeliveryAttempt? attempt = await _attempts.GetByIdAsync(notificationDeliveryAttemptId, cancellationToken);
        if (attempt is null || attempt.Channel != NotificationChannel.Push) return PushNotificationRecipientResolution.Failed("push_recipient_not_available");

        EmergencyContact? contact = await _contacts.GetByIdAsync(attempt.EmergencyContactId, cancellationToken);
        if (contact is null || contact.UserId != attempt.UserId || !contact.IsActive || contact.InvitationStatus != EmergencyContactInvitationStatus.Linked || string.IsNullOrWhiteSpace(contact.LinkedUserId))
        {
            return PushNotificationRecipientResolution.Failed("push_recipient_not_available");
        }

        PushNotificationToken? token = await _tokens.GetLatestActiveFcmByUserIdAsync(contact.LinkedUserId, cancellationToken);
        if (token is null) return PushNotificationRecipientResolution.Failed("push_token_not_available");

        return PushNotificationRecipientResolution.Success(new PushNotificationRecipient(contact.LinkedUserId, token));
    }
}
