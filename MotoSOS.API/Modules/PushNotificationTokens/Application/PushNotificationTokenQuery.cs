using MotoSOS.API.Modules.PushNotificationTokens.Domain;

namespace MotoSOS.API.Modules.PushNotificationTokens.Application;

public sealed record PushNotificationTokenQuery(
    string? UserId,
    PushTokenPlatform? Platform,
    PushTokenChannel? Channel,
    PushNotificationTokenStatus? Status,
    DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo,
    int PageNumber = 1,
    int PageSize = 20);
