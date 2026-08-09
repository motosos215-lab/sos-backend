namespace MotoSOS.API.Modules.PushNotificationTokens.Contracts;

public sealed record PushNotificationTokenResponse(
    string Id,
    string Platform,
    string Channel,
    string? DeviceId,
    string TokenPreview,
    string Status,
    DateTimeOffset RegisteredAtUtc,
    DateTimeOffset LastSeenAtUtc,
    DateTimeOffset? RevokedAtUtc = null);
