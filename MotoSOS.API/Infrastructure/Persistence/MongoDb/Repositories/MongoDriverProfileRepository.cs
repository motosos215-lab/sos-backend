using MongoDB.Driver;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Collections;
using MotoSOS.API.Modules.Profiles.Application;
using MotoSOS.API.Modules.Profiles.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class MongoDriverProfileRepository : IDriverProfileRepository
{
    private readonly IMongoCollection<DriverProfile> _profiles;

    public MongoDriverProfileRepository(IMongoDatabase database)
    {
        _profiles = database.GetCollection<DriverProfile>(MongoCollectionNames.DriverProfiles);
    }

    public async Task<DriverProfile?> GetByUserIdAsync(string userId, CancellationToken cancellationToken)
    {
        return await _profiles.Find(profile => profile.UserId == userId).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(DriverProfile profile, CancellationToken cancellationToken)
    {
        await _profiles.InsertOneAsync(profile, cancellationToken: cancellationToken);
    }

    public async Task UpdateAsync(DriverProfile profile, CancellationToken cancellationToken)
    {
        await _profiles.ReplaceOneAsync(existing => existing.Id == profile.Id, profile, cancellationToken: cancellationToken);
    }
}
