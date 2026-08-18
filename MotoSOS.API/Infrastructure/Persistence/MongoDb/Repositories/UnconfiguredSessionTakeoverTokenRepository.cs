using MotoSOS.API.Modules.Auth.Sessions.Application;
using MotoSOS.API.Modules.Auth.Sessions.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class UnconfiguredSessionTakeoverTokenRepository : ISessionTakeoverTokenRepository
{
    public Task<SessionTakeoverToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) => throw CreateException();
    public Task AddAsync(SessionTakeoverToken token, CancellationToken cancellationToken) => throw CreateException();
    public Task<bool> MarkUsedAsync(string id, DateTimeOffset usedAtUtc, CancellationToken cancellationToken) => throw CreateException();
    private static InvalidOperationException CreateException() => new("MongoDB is not configured for session takeover token persistence.");
}
