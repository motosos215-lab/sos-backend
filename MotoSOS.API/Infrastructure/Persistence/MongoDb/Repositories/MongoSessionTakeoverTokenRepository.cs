using MongoDB.Driver;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Collections;
using MotoSOS.API.Modules.Auth.Sessions.Application;
using MotoSOS.API.Modules.Auth.Sessions.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class MongoSessionTakeoverTokenRepository : ISessionTakeoverTokenRepository
{
    private readonly IMongoCollection<SessionTakeoverToken> _tokens;

    public MongoSessionTakeoverTokenRepository(IMongoDatabase database) => _tokens = database.GetCollection<SessionTakeoverToken>(MongoCollectionNames.SessionTakeoverTokens);

    public async Task<SessionTakeoverToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) => await _tokens.Find(token => token.TokenHash == tokenHash).FirstOrDefaultAsync(cancellationToken);

    public Task AddAsync(SessionTakeoverToken token, CancellationToken cancellationToken) => _tokens.InsertOneAsync(token, cancellationToken: cancellationToken);

    public async Task<bool> MarkUsedAsync(string id, DateTimeOffset usedAtUtc, CancellationToken cancellationToken)
    {
        UpdateResult result = await _tokens.UpdateOneAsync(
            token => token.Id == id && token.UsedAtUtc == null && token.RevokedAtUtc == null,
            Builders<SessionTakeoverToken>.Update.Set(token => token.UsedAtUtc, usedAtUtc),
            cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }
}
