namespace MotoSOS.API.Modules.Notifications.Providers;

public sealed record EmailNotificationMessage(string ToEmail, string Subject, string Body);
