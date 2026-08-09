using MongoDB.Driver;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Collections;
using MotoSOS.API.Modules.AlertAcknowledgements.Domain;
using MotoSOS.API.Modules.AlertDispatch.Domain;
using MotoSOS.API.Modules.EmergencyResolution.Domain;
using MotoSOS.API.Modules.Incidents.Domain;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.OfflineIngestion.Domain;
using MotoSOS.API.Modules.Onboarding.Domain;
using MotoSOS.API.Modules.OperationalDashboard.Application;
using MotoSOS.API.Modules.Trips.Domain;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class MongoOperationalDashboardRepository : IOperationalDashboardRepository
{
    private readonly IMongoCollection<User> _users;
    private readonly IMongoCollection<OnboardingConfirmation> _onboarding;
    private readonly IMongoCollection<Trip> _trips;
    private readonly IMongoCollection<Incident> _incidents;
    private readonly IMongoCollection<AlertDispatchRequest> _alerts;
    private readonly IMongoCollection<NotificationDeliveryAttempt> _notifications;
    private readonly IMongoCollection<AlertAcknowledgement> _acks;
    private readonly IMongoCollection<EmergencyResolutionReport> _reports;
    private readonly IMongoCollection<OfflineIngestionRecord> _offline;

    public MongoOperationalDashboardRepository(IMongoDatabase database)
    {
        _users = database.GetCollection<User>(MongoCollectionNames.Users);
        _onboarding = database.GetCollection<OnboardingConfirmation>(MongoCollectionNames.OnboardingConfirmations);
        _trips = database.GetCollection<Trip>(MongoCollectionNames.Trips);
        _incidents = database.GetCollection<Incident>(MongoCollectionNames.Incidents);
        _alerts = database.GetCollection<AlertDispatchRequest>(MongoCollectionNames.AlertDispatchRequests);
        _notifications = database.GetCollection<NotificationDeliveryAttempt>(MongoCollectionNames.NotificationDeliveryAttempts);
        _acks = database.GetCollection<AlertAcknowledgement>(MongoCollectionNames.AlertAcknowledgements);
        _reports = database.GetCollection<EmergencyResolutionReport>(MongoCollectionNames.EmergencyResolutionReports);
        _offline = database.GetCollection<OfflineIngestionRecord>(MongoCollectionNames.OfflineIngestionRecords);
    }

    public async Task<long> CountUsersAsync(UserRole? role, CancellationToken cancellationToken) => await _users.CountDocumentsAsync(role.HasValue ? Builders<User>.Filter.Eq(u => u.Role, role.Value) : Builders<User>.Filter.Empty, cancellationToken: cancellationToken);
    public async Task<long> CountOperationalOnboardingAsync(CancellationToken cancellationToken) => await _onboarding.CountDocumentsAsync(o => o.IsOperational, cancellationToken: cancellationToken);
    public async Task<long> CountTripsAsync(TripStatus? status, CancellationToken cancellationToken) => await _trips.CountDocumentsAsync(status.HasValue ? Builders<Trip>.Filter.Eq(t => t.Status, status.Value) : Builders<Trip>.Filter.Empty, cancellationToken: cancellationToken);
    public async Task<long> CountIncidentsAsync(IncidentStatus? status, DateTimeOffset? dateFrom, DateTimeOffset? dateTo, CancellationToken cancellationToken) => await _incidents.CountDocumentsAsync(BuildIncidentFilter(status, dateFrom, dateTo), cancellationToken: cancellationToken);
    public async Task<IReadOnlyList<Incident>> ListIncidentsAsync(IncidentStatus? status, DateTimeOffset? dateFrom, DateTimeOffset? dateTo, int pageNumber, int pageSize, CancellationToken cancellationToken) => await _incidents.Find(BuildIncidentFilter(status, dateFrom, dateTo)).SortByDescending(i => i.CreatedAtUtc).Skip((pageNumber - 1) * pageSize).Limit(pageSize).ToListAsync(cancellationToken);
    public async Task<long> CountAlertDispatchesAsync(AlertDispatchStatus? status, CancellationToken cancellationToken) => await _alerts.CountDocumentsAsync(status.HasValue ? Builders<AlertDispatchRequest>.Filter.Eq(a => a.Status, status.Value) : Builders<AlertDispatchRequest>.Filter.Empty, cancellationToken: cancellationToken);
    public async Task<long> CountNotificationsAsync(NotificationDeliveryStatus? status, CancellationToken cancellationToken) => await _notifications.CountDocumentsAsync(status.HasValue ? Builders<NotificationDeliveryAttempt>.Filter.Eq(n => n.Status, status.Value) : Builders<NotificationDeliveryAttempt>.Filter.Empty, cancellationToken: cancellationToken);
    public async Task<long> CountAcknowledgementsAsync(AlertAcknowledgementStatus? status, CancellationToken cancellationToken) => await _acks.CountDocumentsAsync(status.HasValue ? Builders<AlertAcknowledgement>.Filter.Eq(a => a.Status, status.Value) : Builders<AlertAcknowledgement>.Filter.Empty, cancellationToken: cancellationToken);
    public async Task<long> CountResolutionReportsAsync(DateTimeOffset? dateFrom, DateTimeOffset? dateTo, CancellationToken cancellationToken) => await _reports.CountDocumentsAsync(BuildReportFilter(dateFrom, dateTo), cancellationToken: cancellationToken);
    public async Task<IReadOnlyList<EmergencyResolutionReport>> ListResolutionReportsAsync(DateTimeOffset? dateFrom, DateTimeOffset? dateTo, CancellationToken cancellationToken) => await _reports.Find(BuildReportFilter(dateFrom, dateTo)).ToListAsync(cancellationToken);
    public async Task<long> CountOfflineRecordsAsync(OfflineIngestionProcessingStatus status, CancellationToken cancellationToken) => await _offline.CountDocumentsAsync(o => o.ProcessingStatus == status, cancellationToken: cancellationToken);

    private static FilterDefinition<Incident> BuildIncidentFilter(IncidentStatus? status, DateTimeOffset? dateFrom, DateTimeOffset? dateTo)
    {
        var b = Builders<Incident>.Filter; var f = b.Empty;
        if (status.HasValue) f &= b.Eq(i => i.Status, status.Value);
        if (dateFrom.HasValue) f &= b.Gte(i => i.CreatedAtUtc, dateFrom.Value);
        if (dateTo.HasValue) f &= b.Lte(i => i.CreatedAtUtc, dateTo.Value);
        return f;
    }

    private static FilterDefinition<EmergencyResolutionReport> BuildReportFilter(DateTimeOffset? dateFrom, DateTimeOffset? dateTo)
    {
        var b = Builders<EmergencyResolutionReport>.Filter; var f = b.Empty;
        if (dateFrom.HasValue) f &= b.Gte(r => r.CreatedAtUtc, dateFrom.Value);
        if (dateTo.HasValue) f &= b.Lte(r => r.CreatedAtUtc, dateTo.Value);
        return f;
    }
}
