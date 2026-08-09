using MotoSOS.API.Modules.LocationSharing.Domain;

namespace MotoSOS.API.Modules.LocationSharing.Application;

public interface ILocationSharingRepository
{
    Task<EmergencyLocationSnapshot?> GetByUserIdAndIncidentIdAsync(string userId, string incidentId, CancellationToken cancellationToken);
    Task<EmergencyLocationSnapshot?> GetActiveByIncidentIdAsync(string incidentId, CancellationToken cancellationToken);
    Task<EmergencyLocationSnapshot?> GetLatestByIncidentIdAsync(string incidentId, CancellationToken cancellationToken) => GetActiveByIncidentIdAsync(incidentId, cancellationToken);
    Task<EmergencyLocationSnapshot> UpsertLatestAsync(EmergencyLocationSnapshot snapshot, CancellationToken cancellationToken);
}
