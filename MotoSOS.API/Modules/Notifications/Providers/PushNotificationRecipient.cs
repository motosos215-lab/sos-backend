using MotoSOS.API.Modules.PushNotificationTokens.Domain;

namespace MotoSOS.API.Modules.Notifications.Providers;

public sealed record PushNotificationRecipient(string UserId, PushNotificationToken Token);
