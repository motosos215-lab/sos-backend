using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AlertAcknowledgements.Domain;
using MotoSOS.API.Modules.AlertDispatch.Domain;
using MotoSOS.API.Modules.EmergencyResolution.Domain;
using MotoSOS.API.Modules.Incidents.Domain;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.OfflineIngestion.Domain;
using MotoSOS.API.Modules.OperationalDashboard.Contracts;
using MotoSOS.API.Modules.Trips.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Modules.OperationalDashboard.Application;

public sealed class OperationalDashboardService : IOperationalDashboardService
{
    private readonly IUserRepository _users;
    private readonly IOperationalDashboardRepository _dashboard;

    public OperationalDashboardService(IUserRepository users, IOperationalDashboardRepository dashboard)
    {
        _users = users; _dashboard = dashboard;
    }

    public async Task<OperationalDashboardSummaryResponse> GetSummaryAsync(string adminUserId, CancellationToken cancellationToken)
    {
        await EnsureAdminAsync(adminUserId, cancellationToken);
        long riders = await _dashboard.CountUsersAsync(UserRole.Rider, cancellationToken);
        long completedOnboarding = await _dashboard.CountOperationalOnboardingAsync(cancellationToken);
        return new OperationalDashboardSummaryResponse(
            new OperationalDashboardUsersSummaryResponse(await _dashboard.CountUsersAsync(null, cancellationToken), riders, await _dashboard.CountUsersAsync(UserRole.Monitor, cancellationToken), await _dashboard.CountUsersAsync(UserRole.Admin, cancellationToken)),
            new OperationalDashboardOnboardingSummaryResponse(completedOnboarding, Math.Max(0, riders - completedOnboarding)),
            new OperationalDashboardTripsSummaryResponse(await _dashboard.CountTripsAsync(null, cancellationToken), await _dashboard.CountTripsAsync(TripStatus.Active, cancellationToken), await _dashboard.CountTripsAsync(TripStatus.Finished, cancellationToken)),
            new OperationalDashboardIncidentsSummaryResponse(await _dashboard.CountIncidentsAsync(null, null, null, cancellationToken), await _dashboard.CountIncidentsAsync(IncidentStatus.Open, null, null, cancellationToken), await _dashboard.CountIncidentsAsync(IncidentStatus.Closed, null, null, cancellationToken), await _dashboard.CountIncidentsAsync(IncidentStatus.FalsePositiveCancelled, null, null, cancellationToken)),
            new OperationalDashboardAlertsSummaryResponse(await _dashboard.CountAlertDispatchesAsync(null, cancellationToken), await _dashboard.CountAlertDispatchesAsync(AlertDispatchStatus.PendingDispatch, cancellationToken), await _dashboard.CountAlertDispatchesAsync(AlertDispatchStatus.Completed, cancellationToken), await _dashboard.CountAlertDispatchesAsync(AlertDispatchStatus.Cancelled, cancellationToken)),
            new OperationalDashboardNotificationsSummaryResponse(await _dashboard.CountNotificationsAsync(null, cancellationToken), await _dashboard.CountNotificationsAsync(NotificationDeliveryStatus.Prepared, cancellationToken), await _dashboard.CountNotificationsAsync(NotificationDeliveryStatus.SimulatedSent, cancellationToken), await _dashboard.CountNotificationsAsync(NotificationDeliveryStatus.Failed, cancellationToken), await _dashboard.CountNotificationsAsync(NotificationDeliveryStatus.Cancelled, cancellationToken)),
            new OperationalDashboardAcknowledgementsSummaryResponse(await _dashboard.CountAcknowledgementsAsync(null, cancellationToken), await _dashboard.CountAcknowledgementsAsync(AlertAcknowledgementStatus.Pending, cancellationToken), await _dashboard.CountAcknowledgementsAsync(AlertAcknowledgementStatus.Viewed, cancellationToken), await _dashboard.CountAcknowledgementsAsync(AlertAcknowledgementStatus.Acknowledged, cancellationToken), await _dashboard.CountAcknowledgementsAsync(AlertAcknowledgementStatus.Declined, cancellationToken)),
            new OperationalDashboardResolutionReportsSummaryResponse(await _dashboard.CountResolutionReportsAsync(null, null, cancellationToken)),
            await GetOfflineCountsAsync(cancellationToken));
    }

    public async Task<OperationalDashboardIncidentListResponse> ListIncidentsAsync(string adminUserId, OperationalDashboardQuery query, CancellationToken cancellationToken)
    {
        await EnsureAdminAsync(adminUserId, cancellationToken);
        IReadOnlyList<Incident> incidents = await _dashboard.ListIncidentsAsync(query.ParsedStatus, query.DateFrom, query.DateTo, query.PageNumber!.Value, query.PageSize!.Value, cancellationToken);
        long total = await _dashboard.CountIncidentsAsync(query.ParsedStatus, query.DateFrom, query.DateTo, cancellationToken);
        return new OperationalDashboardIncidentListResponse(incidents.Select(ToIncidentItem).ToArray(), query.PageNumber.Value, query.PageSize.Value, total);
    }

    public async Task<OperationalDashboardResponseTimesResponse> GetResponseTimesAsync(string adminUserId, OperationalDashboardQuery query, CancellationToken cancellationToken)
    {
        await EnsureAdminAsync(adminUserId, cancellationToken);
        IReadOnlyList<EmergencyResolutionReport> reports = await _dashboard.ListResolutionReportsAsync(query.DateFrom, query.DateTo, cancellationToken);
        long[] values = reports.Where(r => r.ResponseTimeSeconds.HasValue).Select(r => r.ResponseTimeSeconds!.Value).ToArray();
        return new OperationalDashboardResponseTimesResponse(reports.Count, values.Length, values.Length == 0 ? null : values.Average(), values.Length == 0 ? null : values.Min(), values.Length == 0 ? null : values.Max());
    }

    public async Task<OperationalDashboardResolutionOutcomesResponse> GetResolutionOutcomesAsync(string adminUserId, OperationalDashboardQuery query, CancellationToken cancellationToken)
    {
        await EnsureAdminAsync(adminUserId, cancellationToken);
        IReadOnlyList<EmergencyResolutionReport> reports = await _dashboard.ListResolutionReportsAsync(query.DateFrom, query.DateTo, cancellationToken);
        return new OperationalDashboardResolutionOutcomesResponse(reports.GroupBy(r => r.Outcome).OrderBy(g => g.Key.ToString()).Select(g => new OperationalDashboardResolutionOutcomeItemResponse(g.Key.ToString(), g.LongCount())).ToArray());
    }

    public async Task<OperationalDashboardOfflineProcessingSummaryResponse> GetOfflineProcessingAsync(string adminUserId, CancellationToken cancellationToken)
    {
        await EnsureAdminAsync(adminUserId, cancellationToken);
        return await GetOfflineCountsAsync(cancellationToken);
    }

    private async Task<User> EnsureAdminAsync(string userId, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive) throw new UnauthorizedAppException("Invalid authentication credentials.");
        if (user.Role != UserRole.Admin) throw new ForbiddenAppException("Operational Dashboard API is available only for admins.");
        return user;
    }

    private async Task<OperationalDashboardOfflineProcessingSummaryResponse> GetOfflineCountsAsync(CancellationToken cancellationToken) => new(
        await _dashboard.CountOfflineRecordsAsync(OfflineIngestionProcessingStatus.PendingProcessing, cancellationToken),
        await _dashboard.CountOfflineRecordsAsync(OfflineIngestionProcessingStatus.Processing, cancellationToken),
        await _dashboard.CountOfflineRecordsAsync(OfflineIngestionProcessingStatus.Processed, cancellationToken),
        await _dashboard.CountOfflineRecordsAsync(OfflineIngestionProcessingStatus.Ignored, cancellationToken),
        await _dashboard.CountOfflineRecordsAsync(OfflineIngestionProcessingStatus.FailedPermanent, cancellationToken));

    private static OperationalDashboardIncidentListItemResponse ToIncidentItem(Incident incident) => new(incident.Id, incident.TripId, incident.Status.ToString(), incident.Source.ToString(), incident.Cause.ToString(), incident.RiskLevel.ToString(), incident.OccurredAtUtc, incident.CreatedAtUtc, incident.ClosedAtUtc ?? incident.CancelledAtUtc);
}
