using MotoSOS.API.Modules.PushNotificationTokens.Domain;

namespace MotoSOS.API.Modules.PushNotificationTokens.Application;

public interface IPushNotificationTokenRepository
{
    Task<PushNotificationToken?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task<PushNotificationToken?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);
    Task<(PushNotificationToken Token, bool IsDuplicate)> AddOrGetDuplicateAsync(PushNotificationToken token, CancellationToken cancellationToken);
    Task UpdateAsync(PushNotificationToken token, CancellationToken cancellationToken);
    Task<long> RevokeActiveTokensForScopeAsync(string userId, PushTokenPlatform platform, PushTokenChannel channel, string? deviceId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<long> RevokeActiveTokensBySessionIdAsync(string sessionId, DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult(0L);
    Task<IReadOnlyList<PushNotificationToken>> ListByUserIdAsync(string userId, PushNotificationTokenQuery query, CancellationToken cancellationToken);
    Task<long> CountByUserIdAsync(string userId, PushNotificationTokenQuery query, CancellationToken cancellationToken);
    Task<PushNotificationTokenStatusSummary> GetStatusByUserIdAsync(string userId, CancellationToken cancellationToken);
    Task<PushNotificationToken?> GetLatestActiveFcmByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<PushNotificationToken?>(null);
    Task<IReadOnlyList<PushNotificationToken>> ListAsync(PushNotificationTokenQuery query, CancellationToken cancellationToken);
    Task<long> CountAsync(PushNotificationTokenQuery query, CancellationToken cancellationToken);
}
