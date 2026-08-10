namespace MotoSOS.API.Modules.PushNotificationTokens.Application;

public sealed record PushNotificationTokenStatusSummary(
    long ActiveTokenCount,
    long RevokedTokenCount,
    bool HasActiveAndroidFcm,
    bool HasActiveIosApns,
    bool HasActiveWebPush,
    bool HasActiveWebFcm,
    DateTimeOffset? LastRegisteredAtUtc);
