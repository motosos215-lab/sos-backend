namespace MotoSOS.API.Modules.NotificationOutbox.Contracts;

public sealed record RunNotificationOutboxRequest(int? MaxItems, bool? SimulateFailures);
