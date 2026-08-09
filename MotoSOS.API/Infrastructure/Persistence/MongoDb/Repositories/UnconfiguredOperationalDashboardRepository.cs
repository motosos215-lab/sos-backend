using MotoSOS.API.Modules.AlertAcknowledgements.Domain;
using MotoSOS.API.Modules.AlertDispatch.Domain;
using MotoSOS.API.Modules.EmergencyResolution.Domain;
using MotoSOS.API.Modules.Incidents.Domain;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.OfflineIngestion.Domain;
using MotoSOS.API.Modules.OperationalDashboard.Application;
using MotoSOS.API.Modules.Trips.Domain;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class UnconfiguredOperationalDashboardRepository : IOperationalDashboardRepository
{
    private static InvalidOperationException CreateException() => new("MongoDB is not configured. Configure MongoDB settings to use Operational Dashboard API.");
    public Task<long> CountUsersAsync(UserRole? role, CancellationToken cancellationToken) => throw CreateException();
    public Task<long> CountOperationalOnboardingAsync(CancellationToken cancellationToken) => throw CreateException();
    public Task<long> CountTripsAsync(TripStatus? status, CancellationToken cancellationToken) => throw CreateException();
    public Task<long> CountIncidentsAsync(IncidentStatus? status, DateTimeOffset? dateFrom, DateTimeOffset? dateTo, CancellationToken cancellationToken) => throw CreateException();
    public Task<IReadOnlyList<Incident>> ListIncidentsAsync(IncidentStatus? status, DateTimeOffset? dateFrom, DateTimeOffset? dateTo, int pageNumber, int pageSize, CancellationToken cancellationToken) => throw CreateException();
    public Task<long> CountAlertDispatchesAsync(AlertDispatchStatus? status, CancellationToken cancellationToken) => throw CreateException();
    public Task<long> CountNotificationsAsync(NotificationDeliveryStatus? status, CancellationToken cancellationToken) => throw CreateException();
    public Task<long> CountAcknowledgementsAsync(AlertAcknowledgementStatus? status, CancellationToken cancellationToken) => throw CreateException();
    public Task<long> CountResolutionReportsAsync(DateTimeOffset? dateFrom, DateTimeOffset? dateTo, CancellationToken cancellationToken) => throw CreateException();
    public Task<IReadOnlyList<EmergencyResolutionReport>> ListResolutionReportsAsync(DateTimeOffset? dateFrom, DateTimeOffset? dateTo, CancellationToken cancellationToken) => throw CreateException();
    public Task<long> CountOfflineRecordsAsync(OfflineIngestionProcessingStatus status, CancellationToken cancellationToken) => throw CreateException();
}
