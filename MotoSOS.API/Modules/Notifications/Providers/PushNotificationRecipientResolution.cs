namespace MotoSOS.API.Modules.Notifications.Providers;

public sealed record PushNotificationRecipientResolution(PushNotificationRecipient? Recipient, string? FailureCode)
{
    public static PushNotificationRecipientResolution Success(PushNotificationRecipient recipient) => new(recipient, null);
    public static PushNotificationRecipientResolution Failed(string failureCode) => new(null, failureCode);
}
