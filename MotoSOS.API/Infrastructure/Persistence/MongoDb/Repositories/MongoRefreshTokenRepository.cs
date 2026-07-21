using MongoDB.Driver;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Collections;
using MotoSOS.API.Modules.Auth.Application;
using MotoSOS.API.Modules.Auth.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class MongoRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IMongoCollection<RefreshToken> _refreshTokens;

    public MongoRefreshTokenRepository(IMongoDatabase database)
    {
        _refreshTokens = database.GetCollection<RefreshToken>(MongoCollectionNames.RefreshTokens);
    }

    public async Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        return await _refreshTokens.Find(token => token.TokenHash == tokenHash).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
    {
        await _refreshTokens.InsertOneAsync(refreshToken, cancellationToken: cancellationToken);
    }

    public async Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
    {
        await _refreshTokens.ReplaceOneAsync(existing => existing.Id == refreshToken.Id, refreshToken, cancellationToken: cancellationToken);
    }
}
