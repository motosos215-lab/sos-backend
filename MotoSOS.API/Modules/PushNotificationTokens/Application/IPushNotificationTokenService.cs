using MotoSOS.API.Modules.PushNotificationTokens.Contracts;

namespace MotoSOS.API.Modules.PushNotificationTokens.Application;

public interface IPushNotificationTokenService
{
    Task<RegisterPushNotificationTokenResponse> RegisterAsync(string userId, ValidatedRegisterPushNotificationTokenRequest request, CancellationToken cancellationToken);
    Task<GetPushNotificationTokensResponse> ListMineAsync(string userId, PushNotificationTokenQuery query, CancellationToken cancellationToken);
    Task<PushNotificationTokenStatusResponse> GetStatusAsync(string userId, CancellationToken cancellationToken);
    Task<PushNotificationTokenResponse> RevokeMineAsync(string userId, string id, CancellationToken cancellationToken);
    Task<GetPushNotificationTokensResponse> ListForAdminAsync(string adminUserId, PushNotificationTokenQuery query, CancellationToken cancellationToken);
    Task<PushNotificationTokenResponse> RevokeForAdminAsync(string adminUserId, string id, CancellationToken cancellationToken);
}
