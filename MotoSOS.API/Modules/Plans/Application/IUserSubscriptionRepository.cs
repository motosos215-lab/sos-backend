using MotoSOS.API.Modules.Plans.Domain;

namespace MotoSOS.API.Modules.Plans.Application;

public interface IUserSubscriptionRepository
{
    Task<UserSubscription?> GetByUserIdAsync(string userId, CancellationToken cancellationToken);
    Task<bool> HasActiveSubscriptionAsync(string userId, CancellationToken cancellationToken);
    Task AddAsync(UserSubscription subscription, CancellationToken cancellationToken);
    Task UpdateAsync(UserSubscription subscription, CancellationToken cancellationToken);
}
