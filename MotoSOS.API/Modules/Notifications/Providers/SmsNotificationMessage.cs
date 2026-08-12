namespace MotoSOS.API.Modules.Notifications.Providers;

public sealed record SmsNotificationMessage(string ToPhoneNumber, string Body, string Sender);
