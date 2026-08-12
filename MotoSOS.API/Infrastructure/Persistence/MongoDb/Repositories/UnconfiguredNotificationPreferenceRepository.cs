using MotoSOS.API.Modules.NotificationPreferences.Application;
using MotoSOS.API.Modules.NotificationPreferences.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class UnconfiguredNotificationPreferenceRepository : INotificationPreferenceRepository
{
    public Task<NotificationPreference?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) => throw CreateException();

    public Task AddAsync(NotificationPreference preference, CancellationToken cancellationToken) => throw CreateException();

    public Task UpdateAsync(NotificationPreference preference, CancellationToken cancellationToken) => throw CreateException();

    private static InvalidOperationException CreateException() => new("MongoDB is not configured for notification preference persistence.");
}
