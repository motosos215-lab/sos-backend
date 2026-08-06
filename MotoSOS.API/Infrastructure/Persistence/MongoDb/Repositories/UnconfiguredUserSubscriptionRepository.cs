using MotoSOS.API.Modules.Plans.Application;
using MotoSOS.API.Modules.Plans.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class UnconfiguredUserSubscriptionRepository : IUserSubscriptionRepository
{
    public Task<UserSubscription?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) => throw CreateException();
    public Task<bool> HasActiveSubscriptionAsync(string userId, CancellationToken cancellationToken) => throw CreateException();
    public Task AddAsync(UserSubscription subscription, CancellationToken cancellationToken) => throw CreateException();
    public Task UpdateAsync(UserSubscription subscription, CancellationToken cancellationToken) => throw CreateException();
    private static InvalidOperationException CreateException() => new("MongoDB is not configured.");
}
