using MotoSOS.API.Modules.Auth.Application;
using MotoSOS.API.Modules.Auth.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class UnconfiguredAuthCodeRepository : IAuthCodeRepository
{
    public Task<AuthCode?> GetLatestByEmailAndPurposeAsync(string emailNormalized, AuthCodePurpose purpose, CancellationToken cancellationToken) => throw CreateException();

    public Task AddAsync(AuthCode authCode, CancellationToken cancellationToken) => throw CreateException();

    public Task UpdateAsync(AuthCode authCode, CancellationToken cancellationToken) => throw CreateException();

    public Task RevokeActiveAsync(string emailNormalized, AuthCodePurpose purpose, DateTimeOffset revokedAtUtc, CancellationToken cancellationToken) => throw CreateException();

    private static InvalidOperationException CreateException() => new("MongoDB is not configured for auth code persistence.");
}
