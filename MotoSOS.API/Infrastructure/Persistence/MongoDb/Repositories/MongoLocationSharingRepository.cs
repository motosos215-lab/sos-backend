using MongoDB.Driver;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Collections;
using MotoSOS.API.Modules.LocationSharing.Application;
using MotoSOS.API.Modules.LocationSharing.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class MongoLocationSharingRepository : ILocationSharingRepository
{
    private readonly IMongoCollection<EmergencyLocationSnapshot> _locations;
    public MongoLocationSharingRepository(IMongoDatabase database) => _locations = database.GetCollection<EmergencyLocationSnapshot>(MongoCollectionNames.EmergencyLocationSnapshots);
    public async Task<EmergencyLocationSnapshot?> GetByUserIdAndIncidentIdAsync(string userId, string incidentId, CancellationToken cancellationToken) => await _locations.Find(l => l.UserId == userId && l.IncidentId == incidentId).FirstOrDefaultAsync(cancellationToken);
    public async Task<EmergencyLocationSnapshot?> GetActiveByIncidentIdAsync(string incidentId, CancellationToken cancellationToken) => await _locations.Find(l => l.IncidentId == incidentId && l.IsActive).FirstOrDefaultAsync(cancellationToken);
    public async Task<EmergencyLocationSnapshot> UpsertLatestAsync(EmergencyLocationSnapshot snapshot, CancellationToken cancellationToken)
    {
        await _locations.ReplaceOneAsync(l => l.UserId == snapshot.UserId && l.IncidentId == snapshot.IncidentId, snapshot, new ReplaceOptions { IsUpsert = true }, cancellationToken);
        return snapshot;
    }
}
