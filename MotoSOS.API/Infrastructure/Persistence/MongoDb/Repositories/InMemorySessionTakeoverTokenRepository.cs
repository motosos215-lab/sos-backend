using MotoSOS.API.Modules.Auth.Sessions.Application;
using MotoSOS.API.Modules.Auth.Sessions.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class InMemorySessionTakeoverTokenRepository : ISessionTakeoverTokenRepository
{
    private readonly object _sync = new();
    private readonly List<SessionTakeoverToken> _items = [];

    public Task<SessionTakeoverToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        lock (_sync) return Task.FromResult(_items.FirstOrDefault(token => token.TokenHash == tokenHash));
    }

    public Task AddAsync(SessionTakeoverToken token, CancellationToken cancellationToken)
    {
        lock (_sync) _items.Add(token);
        return Task.CompletedTask;
    }

    public Task<bool> MarkUsedAsync(string id, DateTimeOffset usedAtUtc, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            SessionTakeoverToken? token = _items.FirstOrDefault(item => item.Id == id && item.UsedAtUtc is null && item.RevokedAtUtc is null);
            if (token is null) return Task.FromResult(false);
            token.UsedAtUtc = usedAtUtc;
            return Task.FromResult(true);
        }
    }
}
