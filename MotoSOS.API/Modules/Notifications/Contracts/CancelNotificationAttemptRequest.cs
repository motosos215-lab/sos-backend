namespace MotoSOS.API.Modules.Notifications.Contracts;

public sealed record CancelNotificationAttemptRequest(string? Reason, DateTimeOffset? ClientCancelledAtUtc);
