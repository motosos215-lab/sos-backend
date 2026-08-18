using MotoSOS.API.Modules.Auth.Sessions.Application;
using MotoSOS.API.Modules.Auth.Sessions.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class UnconfiguredUserSessionRepository : IUserSessionRepository
{
    public Task<UserSession?> GetByIdAsync(string id, CancellationToken cancellationToken) => throw CreateException();
    public Task<UserSession?> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => throw CreateException();
    public Task<UserSession?> GetActiveByUserIdAndSessionTypeAsync(string userId, UserSessionType sessionType, CancellationToken cancellationToken) => throw CreateException();
    public Task<(UserSession Session, bool Created)> AddActiveOrGetExistingAsync(UserSession session, CancellationToken cancellationToken) => throw CreateException();
    public Task UpdateAsync(UserSession session, CancellationToken cancellationToken) => throw CreateException();
    private static InvalidOperationException CreateException() => new("MongoDB is not configured for user session persistence.");
}
