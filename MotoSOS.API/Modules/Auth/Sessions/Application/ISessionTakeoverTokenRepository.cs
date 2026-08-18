using MotoSOS.API.Modules.Auth.Sessions.Domain;

namespace MotoSOS.API.Modules.Auth.Sessions.Application;

public interface ISessionTakeoverTokenRepository
{
    Task<SessionTakeoverToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken);
    Task AddAsync(SessionTakeoverToken token, CancellationToken cancellationToken);
    Task<bool> MarkUsedAsync(string id, DateTimeOffset usedAtUtc, CancellationToken cancellationToken);
}
