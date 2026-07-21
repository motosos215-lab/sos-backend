using MotoSOS.API.Modules.Auth.Application;
using MotoSOS.API.Modules.Auth.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class UnconfiguredRefreshTokenRepository : IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) => throw CreateException();

    public Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken) => throw CreateException();

    public Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken) => throw CreateException();

    private static InvalidOperationException CreateException() => new("MongoDB is not configured for refresh token persistence.");
}
