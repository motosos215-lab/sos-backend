namespace MotoSOS.API.Modules.PushNotificationTokens.Contracts;

public sealed record GetPushNotificationTokensResponse(IReadOnlyList<PushNotificationTokenResponse> PushNotificationTokens, int PageNumber, int PageSize, long TotalCount);
