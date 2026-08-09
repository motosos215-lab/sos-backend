namespace MotoSOS.API.Modules.Notifications.Providers;

public sealed record FcmPushResult(bool Success, string? MessageId, string? FailureCode, string? FailureMessage);
