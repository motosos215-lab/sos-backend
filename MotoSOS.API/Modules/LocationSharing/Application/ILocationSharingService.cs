using MotoSOS.API.Modules.LocationSharing.Contracts;

namespace MotoSOS.API.Modules.LocationSharing.Application;

public interface ILocationSharingService
{
    Task<ShareLocationSnapshotResponse> ShareAsync(string userId, ShareLocationSnapshotRequest request, CancellationToken cancellationToken);
    Task<GetLocationSnapshotResponse> GetForMonitorAsync(string monitorUserId, string notificationDeliveryAttemptId, CancellationToken cancellationToken);
    Task<GetLocationSnapshotResponse> GetForRiderAsync(string riderUserId, string incidentId, CancellationToken cancellationToken);
}
