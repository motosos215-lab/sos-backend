using MongoDB.Bson;
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
        await EnsureIndexAsync(users, "ux_users_email", new BsonDocument(nameof(User.Email), 1), unique: true, cancellationToken);

        IMongoCollection<RefreshToken> refreshTokens = _database.GetCollection<RefreshToken>(MongoCollectionNames.RefreshTokens);
        await EnsureIndexAsync(refreshTokens, "ux_refreshTokens_tokenHash", new BsonDocument(nameof(RefreshToken.TokenHash), 1), unique: true, cancellationToken);
        await EnsureIndexAsync(refreshTokens, "ix_refreshTokens_userId", new BsonDocument(nameof(RefreshToken.UserId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(
            refreshTokens,
            "ix_refreshTokens_userId_revokedAtUtc_expiresAtUtc",
            new BsonDocument
            {
                [nameof(RefreshToken.UserId)] = 1,
                [nameof(RefreshToken.RevokedAtUtc)] = 1,
                [nameof(RefreshToken.ExpiresAtUtc)] = 1
            },
            unique: false,
            cancellationToken);
        await EnsureIndexAsync(
            refreshTokens,
            "ix_refreshTokens_userId_expiresAtUtc",
            new BsonDocument
            {
                [nameof(RefreshToken.UserId)] = 1,
                [nameof(RefreshToken.ExpiresAtUtc)] = 1
            },
            unique: false,
            cancellationToken);
    }

    private static async Task EnsureIndexAsync<TDocument>(
        IMongoCollection<TDocument> collection,
        string name,
        BsonDocument key,
        bool unique,
        CancellationToken cancellationToken)
    {
        if (await HasEquivalentIndexAsync(collection, key, unique, cancellationToken))
        {
            return;
        }

        var index = new CreateIndexModel<TDocument>(
            new BsonDocumentIndexKeysDefinition<TDocument>(key),
            new CreateIndexOptions { Name = name, Unique = unique });

        try
        {
            await collection.Indexes.CreateOneAsync(index, cancellationToken: cancellationToken);
        }
        catch (MongoCommandException exception) when (IsIndexConflict(exception))
        {
            if (await HasEquivalentIndexAsync(collection, key, unique, cancellationToken))
            {
                return;
            }

            throw;
        }
    }

    private static async Task<bool> HasEquivalentIndexAsync<TDocument>(
        IMongoCollection<TDocument> collection,
        BsonDocument key,
        bool unique,
        CancellationToken cancellationToken)
    {
        using IAsyncCursor<BsonDocument> cursor = await collection.Indexes.ListAsync(cancellationToken: cancellationToken);

        while (await cursor.MoveNextAsync(cancellationToken))
        {
            foreach (BsonDocument existingIndex in cursor.Current)
            {
                if (IsEquivalentIndex(existingIndex, key, unique))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsEquivalentIndex(BsonDocument existingIndex, BsonDocument key, bool unique)
    {
        if (!existingIndex.TryGetValue("key", out BsonValue existingKey) || !existingKey.IsBsonDocument)
        {
            return false;
        }

        bool existingUnique = existingIndex.TryGetValue("unique", out BsonValue uniqueValue) && uniqueValue.ToBoolean();

        return existingKey.AsBsonDocument.Equals(key) && existingUnique == unique;
    }

    private static bool IsIndexConflict(MongoCommandException exception)
    {
        return exception.Code is 85 or 86 ||
            exception.Message.Contains("Index already exists with a different name", StringComparison.OrdinalIgnoreCase);
    }
}
