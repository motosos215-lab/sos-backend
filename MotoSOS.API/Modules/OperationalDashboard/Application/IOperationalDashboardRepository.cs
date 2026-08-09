using MotoSOS.API.Modules.AlertAcknowledgements.Domain;
using MotoSOS.API.Modules.AlertDispatch.Domain;
using MotoSOS.API.Modules.EmergencyResolution.Domain;
using MotoSOS.API.Modules.Incidents.Domain;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.OfflineIngestion.Domain;
using MotoSOS.API.Modules.OperationalDashboard.Contracts;
using MotoSOS.API.Modules.Trips.Domain;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Modules.OperationalDashboard.Application;

public interface IOperationalDashboardRepository
{
    Task<long> CountUsersAsync(UserRole? role, CancellationToken cancellationToken);
    Task<long> CountOperationalOnboardingAsync(CancellationToken cancellationToken);
    Task<long> CountTripsAsync(TripStatus? status, CancellationToken cancellationToken);
    Task<long> CountIncidentsAsync(IncidentStatus? status, DateTimeOffset? dateFrom, DateTimeOffset? dateTo, CancellationToken cancellationToken);
    Task<IReadOnlyList<Incident>> ListIncidentsAsync(IncidentStatus? status, DateTimeOffset? dateFrom, DateTimeOffset? dateTo, int pageNumber, int pageSize, CancellationToken cancellationToken);
    Task<long> CountAlertDispatchesAsync(AlertDispatchStatus? status, CancellationToken cancellationToken);
    Task<long> CountNotificationsAsync(NotificationDeliveryStatus? status, CancellationToken cancellationToken);
    Task<long> CountAcknowledgementsAsync(AlertAcknowledgementStatus? status, CancellationToken cancellationToken);
    Task<long> CountResolutionReportsAsync(DateTimeOffset? dateFrom, DateTimeOffset? dateTo, CancellationToken cancellationToken);
    Task<IReadOnlyList<EmergencyResolutionReport>> ListResolutionReportsAsync(DateTimeOffset? dateFrom, DateTimeOffset? dateTo, CancellationToken cancellationToken);
    Task<long> CountOfflineRecordsAsync(OfflineIngestionProcessingStatus status, CancellationToken cancellationToken);
}
