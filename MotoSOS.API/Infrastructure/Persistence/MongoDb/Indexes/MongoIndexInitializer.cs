using MongoDB.Driver;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Collections;
using MotoSOS.API.Modules.Auth.Domain;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Indexes;

public sealed class MongoIndexInitializer
{
    private readonly IMongoDatabase _database;

    public MongoIndexInitializer(IMongoDatabase database)
    {
        _database = database;
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        IMongoCollection<User> users = _database.GetCollection<User>(MongoCollectionNames.Users);
        var emailIndex = new CreateIndexModel<User>(
            Builders<User>.IndexKeys.Ascending(user => user.Email),
            new CreateIndexOptions { Unique = true });

        await users.Indexes.CreateOneAsync(emailIndex, cancellationToken: cancellationToken);

        IMongoCollection<RefreshToken> refreshTokens = _database.GetCollection<RefreshToken>(MongoCollectionNames.RefreshTokens);
        var tokenHashIndex = new CreateIndexModel<RefreshToken>(
            Builders<RefreshToken>.IndexKeys.Ascending(token => token.TokenHash),
            new CreateIndexOptions { Unique = true });
        var userExpirationIndex = new CreateIndexModel<RefreshToken>(
            Builders<RefreshToken>.IndexKeys.Ascending(token => token.UserId).Ascending(token => token.ExpiresAtUtc));

        await refreshTokens.Indexes.CreateManyAsync([tokenHashIndex, userExpirationIndex], cancellationToken: cancellationToken);
    }
}
