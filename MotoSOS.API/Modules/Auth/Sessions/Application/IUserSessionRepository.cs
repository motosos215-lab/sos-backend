using MotoSOS.API.Modules.Auth.Sessions.Domain;

namespace MotoSOS.API.Modules.Auth.Sessions.Application;

public interface IUserSessionRepository
{
    Task<UserSession?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task<UserSession?> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken);
    Task<UserSession?> GetActiveByUserIdAndSessionTypeAsync(string userId, UserSessionType sessionType, CancellationToken cancellationToken);
    Task<(UserSession Session, bool Created)> AddActiveOrGetExistingAsync(UserSession session, CancellationToken cancellationToken);
    Task UpdateAsync(UserSession session, CancellationToken cancellationToken);
}
