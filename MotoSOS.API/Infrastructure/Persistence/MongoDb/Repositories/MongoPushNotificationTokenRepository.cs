using MongoDB.Driver;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Collections;
using MotoSOS.API.Modules.PushNotificationTokens.Application;
using MotoSOS.API.Modules.PushNotificationTokens.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class MongoPushNotificationTokenRepository : IPushNotificationTokenRepository
{
    private readonly IMongoCollection<PushNotificationToken> _tokens;

    public MongoPushNotificationTokenRepository(IMongoDatabase database) => _tokens = database.GetCollection<PushNotificationToken>(MongoCollectionNames.PushNotificationTokens);

    public async Task<PushNotificationToken?> GetByIdAsync(string id, CancellationToken cancellationToken) => await _tokens.Find(token => token.Id == id).FirstOrDefaultAsync(cancellationToken);
    public async Task<PushNotificationToken?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) => await _tokens.Find(token => token.IdempotencyKey == idempotencyKey).FirstOrDefaultAsync(cancellationToken);

    public async Task<(PushNotificationToken Token, bool IsDuplicate)> AddOrGetDuplicateAsync(PushNotificationToken token, CancellationToken cancellationToken)
    {
        PushNotificationToken? existing = await GetByIdempotencyKeyAsync(token.IdempotencyKey, cancellationToken);
        if (existing is not null) return (existing, true);

        try
        {
            await _tokens.InsertOneAsync(token, cancellationToken: cancellationToken);
            return (token, false);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            existing = await GetByIdempotencyKeyAsync(token.IdempotencyKey, cancellationToken);
            if (existing is not null) return (existing, true);
            throw;
        }
    }

    public Task UpdateAsync(PushNotificationToken token, CancellationToken cancellationToken) => _tokens.ReplaceOneAsync(existing => existing.Id == token.Id, token, cancellationToken: cancellationToken);

    public async Task<long> RevokeActiveTokensForScopeAsync(string userId, PushTokenPlatform platform, PushTokenChannel channel, string? deviceId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        FilterDefinitionBuilder<PushNotificationToken> b = Builders<PushNotificationToken>.Filter;
        FilterDefinition<PushNotificationToken> filter = b.Eq(token => token.UserId, userId) & b.Eq(token => token.Platform, platform) & b.Eq(token => token.Channel, channel) & b.Eq(token => token.Status, PushNotificationTokenStatus.Active);
        filter &= string.IsNullOrWhiteSpace(deviceId) ? b.Eq(token => token.DeviceId, null) : b.Eq(token => token.DeviceId, deviceId);
        UpdateDefinition<PushNotificationToken> update = Builders<PushNotificationToken>.Update.Set(token => token.Status, PushNotificationTokenStatus.Revoked).Set(token => token.RevokedAtUtc, now).Set(token => token.UpdatedAtUtc, now);
        UpdateResult result = await _tokens.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount;
    }

    public async Task<long> RevokeActiveTokensBySessionIdAsync(string sessionId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        UpdateDefinition<PushNotificationToken> update = Builders<PushNotificationToken>.Update
            .Set(token => token.Status, PushNotificationTokenStatus.Revoked)
            .Set(token => token.RevokedAtUtc, now)
            .Set(token => token.UpdatedAtUtc, now);
        UpdateResult result = await _tokens.UpdateManyAsync(
            token => token.SessionId == sessionId && token.Status == PushNotificationTokenStatus.Active,
            update,
            cancellationToken: cancellationToken);
        return result.ModifiedCount;
    }

    public async Task<IReadOnlyList<PushNotificationToken>> ListByUserIdAsync(string userId, PushNotificationTokenQuery query, CancellationToken cancellationToken) => await _tokens.Find(BuildFilter(query) & Builders<PushNotificationToken>.Filter.Eq(token => token.UserId, userId)).SortByDescending(token => token.LastSeenAtUtc).Skip((query.PageNumber - 1) * query.PageSize).Limit(query.PageSize).ToListAsync(cancellationToken);
    public async Task<long> CountByUserIdAsync(string userId, PushNotificationTokenQuery query, CancellationToken cancellationToken) => await _tokens.CountDocumentsAsync(BuildFilter(query) & Builders<PushNotificationToken>.Filter.Eq(token => token.UserId, userId), cancellationToken: cancellationToken);
    public async Task<IReadOnlyList<PushNotificationToken>> ListAsync(PushNotificationTokenQuery query, CancellationToken cancellationToken) => await _tokens.Find(BuildFilter(query)).SortByDescending(token => token.LastSeenAtUtc).Skip((query.PageNumber - 1) * query.PageSize).Limit(query.PageSize).ToListAsync(cancellationToken);
    public async Task<long> CountAsync(PushNotificationTokenQuery query, CancellationToken cancellationToken) => await _tokens.CountDocumentsAsync(BuildFilter(query), cancellationToken: cancellationToken);

    public async Task<PushNotificationTokenStatusSummary> GetStatusByUserIdAsync(string userId, CancellationToken cancellationToken)
    {
        IReadOnlyList<PushNotificationToken> items = await _tokens.Find(token => token.UserId == userId).ToListAsync(cancellationToken);
        PushNotificationToken[] active = items.Where(token => token.Status == PushNotificationTokenStatus.Active).ToArray();
        return new PushNotificationTokenStatusSummary(
            active.Length,
            items.Count(token => token.Status == PushNotificationTokenStatus.Revoked),
            active.Any(token => token.Platform == PushTokenPlatform.Android && token.Channel == PushTokenChannel.Fcm),
            active.Any(token => token.Platform == PushTokenPlatform.Ios && token.Channel == PushTokenChannel.Apns),
            active.Any(token => token.Platform == PushTokenPlatform.Web && token.Channel == PushTokenChannel.WebPush),
            active.Any(token => token.Platform == PushTokenPlatform.Web && token.Channel == PushTokenChannel.Fcm),
            items.OrderByDescending(token => token.RegisteredAtUtc).FirstOrDefault()?.RegisteredAtUtc);
    }

    public async Task<PushNotificationToken?> GetLatestActiveFcmByUserIdAsync(string userId, CancellationToken cancellationToken)
    {
        FilterDefinitionBuilder<PushNotificationToken> b = Builders<PushNotificationToken>.Filter;
        FilterDefinition<PushNotificationToken> filter = b.Eq(token => token.UserId, userId) &
            b.Eq(token => token.Status, PushNotificationTokenStatus.Active) &
            b.Eq(token => token.Channel, PushTokenChannel.Fcm) &
            b.In(token => token.Platform, [PushTokenPlatform.Android, PushTokenPlatform.Web]);
        return await _tokens.Find(filter).SortByDescending(token => token.LastSeenAtUtc).FirstOrDefaultAsync(cancellationToken);
    }

    private static FilterDefinition<PushNotificationToken> BuildFilter(PushNotificationTokenQuery query)
    {
        FilterDefinitionBuilder<PushNotificationToken> b = Builders<PushNotificationToken>.Filter;
        FilterDefinition<PushNotificationToken> f = b.Empty;
        if (!string.IsNullOrWhiteSpace(query.UserId)) f &= b.Eq(token => token.UserId, query.UserId);
        if (query.Platform.HasValue) f &= b.Eq(token => token.Platform, query.Platform.Value);
        if (query.Channel.HasValue) f &= b.Eq(token => token.Channel, query.Channel.Value);
        if (query.Status.HasValue) f &= b.Eq(token => token.Status, query.Status.Value);
        if (query.DateFrom.HasValue) f &= b.Gte(token => token.RegisteredAtUtc, query.DateFrom.Value);
        if (query.DateTo.HasValue) f &= b.Lte(token => token.RegisteredAtUtc, query.DateTo.Value);
        return f;
    }
}
