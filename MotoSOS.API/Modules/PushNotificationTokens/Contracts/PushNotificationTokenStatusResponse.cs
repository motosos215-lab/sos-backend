namespace MotoSOS.API.Modules.PushNotificationTokens.Contracts;

public sealed record PushNotificationTokenStatusResponse(
    long ActiveTokenCount,
    long RevokedTokenCount,
    bool HasActiveAndroidFcm,
    bool HasActiveIosApns,
    bool HasActiveWebPush,
    bool HasActiveWebFcm,
    DateTimeOffset? LastRegisteredAtUtc);
