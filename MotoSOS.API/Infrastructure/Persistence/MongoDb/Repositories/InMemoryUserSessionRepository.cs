using MotoSOS.API.Modules.Auth.Sessions.Application;
using MotoSOS.API.Modules.Auth.Sessions.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class InMemoryUserSessionRepository : IUserSessionRepository
{
    private readonly object _sync = new();
    private readonly List<UserSession> _items = [];

    public Task<UserSession?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        lock (_sync) return Task.FromResult(_items.FirstOrDefault(session => session.Id == id));
    }

    public Task<UserSession?> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken)
    {
        lock (_sync) return Task.FromResult(_items.FirstOrDefault(session => session.UserId == userId && session.RevokedAtUtc is null));
    }

    public Task<UserSession?> GetActiveByUserIdAndSessionTypeAsync(string userId, UserSessionType sessionType, CancellationToken cancellationToken)
    {
        lock (_sync) return Task.FromResult(_items.FirstOrDefault(session => session.UserId == userId && session.SessionType == sessionType && session.RevokedAtUtc is null));
    }

    public Task<(UserSession Session, bool Created)> AddActiveOrGetExistingAsync(UserSession session, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            UserSession? existing = _items.FirstOrDefault(item => item.UserId == session.UserId && item.SessionType == session.SessionType && item.RevokedAtUtc is null);
            if (existing is not null) return Task.FromResult((existing, false));
            _items.Add(session);
            return Task.FromResult((session, true));
        }
    }

    public Task UpdateAsync(UserSession session, CancellationToken cancellationToken) => Task.CompletedTask;
}
