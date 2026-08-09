using MotoSOS.API.Modules.LocationSharing.Application;
using MotoSOS.API.Modules.LocationSharing.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class UnconfiguredLocationSharingRepository : ILocationSharingRepository
{
    private static InvalidOperationException CreateException() => new("MongoDB is not configured. Configure MongoDb:ConnectionString and MongoDb:DatabaseName to use Location Sharing API.");
    public Task<EmergencyLocationSnapshot?> GetByUserIdAndIncidentIdAsync(string userId, string incidentId, CancellationToken cancellationToken) => throw CreateException();
    public Task<EmergencyLocationSnapshot?> GetActiveByIncidentIdAsync(string incidentId, CancellationToken cancellationToken) => throw CreateException();
    public Task<EmergencyLocationSnapshot> UpsertLatestAsync(EmergencyLocationSnapshot snapshot, CancellationToken cancellationToken) => throw CreateException();
}
