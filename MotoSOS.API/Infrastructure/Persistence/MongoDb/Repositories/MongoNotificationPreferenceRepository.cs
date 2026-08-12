using MongoDB.Driver;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Collections;
using MotoSOS.API.Modules.NotificationPreferences.Application;
using MotoSOS.API.Modules.NotificationPreferences.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class MongoNotificationPreferenceRepository : INotificationPreferenceRepository
{
    private readonly IMongoCollection<NotificationPreference> _preferences;

    public MongoNotificationPreferenceRepository(IMongoDatabase database)
    {
        _preferences = database.GetCollection<NotificationPreference>(MongoCollectionNames.NotificationPreferences);
    }

    public async Task<NotificationPreference?> GetByUserIdAsync(string userId, CancellationToken cancellationToken)
    {
        return await _preferences.Find(preference => preference.UserId == userId).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(NotificationPreference preference, CancellationToken cancellationToken)
    {
        await _preferences.InsertOneAsync(preference, cancellationToken: cancellationToken);
    }

    public async Task UpdateAsync(NotificationPreference preference, CancellationToken cancellationToken)
    {
        await _preferences.ReplaceOneAsync(existing => existing.Id == preference.Id, preference, cancellationToken: cancellationToken);
    }
}
