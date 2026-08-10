using MotoSOS.API.Modules.PushNotificationTokens.Application;
using MotoSOS.API.Modules.PushNotificationTokens.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class UnconfiguredPushNotificationTokenRepository : IPushNotificationTokenRepository
{
    private static InvalidOperationException CreateException() => new("MongoDB is not configured. Configure MongoDB settings to use Push Notification Tokens API.");
    public Task<PushNotificationToken?> GetByIdAsync(string id, CancellationToken cancellationToken) => throw CreateException();
    public Task<PushNotificationToken?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) => throw CreateException();
    public Task<(PushNotificationToken Token, bool IsDuplicate)> AddOrGetDuplicateAsync(PushNotificationToken token, CancellationToken cancellationToken) => throw CreateException();
    public Task UpdateAsync(PushNotificationToken token, CancellationToken cancellationToken) => throw CreateException();
    public Task<long> RevokeActiveTokensForScopeAsync(string userId, PushTokenPlatform platform, PushTokenChannel channel, string? deviceId, DateTimeOffset now, CancellationToken cancellationToken) => throw CreateException();
    public Task<IReadOnlyList<PushNotificationToken>> ListByUserIdAsync(string userId, PushNotificationTokenQuery query, CancellationToken cancellationToken) => throw CreateException();
    public Task<long> CountByUserIdAsync(string userId, PushNotificationTokenQuery query, CancellationToken cancellationToken) => throw CreateException();
    public Task<PushNotificationTokenStatusSummary> GetStatusByUserIdAsync(string userId, CancellationToken cancellationToken) => throw CreateException();
    public Task<PushNotificationToken?> GetLatestActiveFcmByUserIdAsync(string userId, CancellationToken cancellationToken) => throw CreateException();
    public Task<IReadOnlyList<PushNotificationToken>> ListAsync(PushNotificationTokenQuery query, CancellationToken cancellationToken) => throw CreateException();
    public Task<long> CountAsync(PushNotificationTokenQuery query, CancellationToken cancellationToken) => throw CreateException();
}
