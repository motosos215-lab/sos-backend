using MongoDB.Driver;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Collections;
using MotoSOS.API.Modules.Plans.Application;
using MotoSOS.API.Modules.Plans.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class MongoUserSubscriptionRepository : IUserSubscriptionRepository
{
    private readonly IMongoCollection<UserSubscription> _subscriptions;

    public MongoUserSubscriptionRepository(IMongoDatabase database)
    {
        _subscriptions = database.GetCollection<UserSubscription>(MongoCollectionNames.UserSubscriptions);
    }

    public async Task<UserSubscription?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) =>
        await _subscriptions.Find(subscription => subscription.UserId == userId).SortByDescending(subscription => subscription.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> HasActiveSubscriptionAsync(string userId, CancellationToken cancellationToken) =>
        await _subscriptions.Find(subscription => subscription.UserId == userId && subscription.Status == SubscriptionStatus.Active).AnyAsync(cancellationToken);

    public async Task AddAsync(UserSubscription subscription, CancellationToken cancellationToken) =>
        await _subscriptions.InsertOneAsync(subscription, cancellationToken: cancellationToken);

    public async Task UpdateAsync(UserSubscription subscription, CancellationToken cancellationToken) =>
        await _subscriptions.ReplaceOneAsync(existing => existing.Id == subscription.Id, subscription, cancellationToken: cancellationToken);
}
