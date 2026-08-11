using MongoDB.Driver;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Collections;
using MotoSOS.API.Modules.Auth.Application;
using MotoSOS.API.Modules.Auth.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class MongoAuthCodeRepository : IAuthCodeRepository
{
    private readonly IMongoCollection<AuthCode> _authCodes;

    public MongoAuthCodeRepository(IMongoDatabase database)
    {
        _authCodes = database.GetCollection<AuthCode>(MongoCollectionNames.AuthCodes);
    }

    public async Task<AuthCode?> GetLatestByEmailAndPurposeAsync(string emailNormalized, AuthCodePurpose purpose, CancellationToken cancellationToken)
    {
        return await _authCodes
            .Find(code => code.EmailNormalized == emailNormalized && code.Purpose == purpose)
            .SortByDescending(code => code.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(AuthCode authCode, CancellationToken cancellationToken)
    {
        await _authCodes.InsertOneAsync(authCode, cancellationToken: cancellationToken);
    }

    public async Task UpdateAsync(AuthCode authCode, CancellationToken cancellationToken)
    {
        await _authCodes.ReplaceOneAsync(existing => existing.Id == authCode.Id, authCode, cancellationToken: cancellationToken);
    }

    public async Task RevokeActiveAsync(string emailNormalized, AuthCodePurpose purpose, DateTimeOffset revokedAtUtc, CancellationToken cancellationToken)
    {
        var update = Builders<AuthCode>.Update
            .Set(code => code.Status, AuthCodeStatus.Revoked)
            .Set(code => code.RevokedAtUtc, revokedAtUtc);

        await _authCodes.UpdateManyAsync(
            code => code.EmailNormalized == emailNormalized && code.Purpose == purpose && code.Status == AuthCodeStatus.Active,
            update,
            cancellationToken: cancellationToken);
    }
}
