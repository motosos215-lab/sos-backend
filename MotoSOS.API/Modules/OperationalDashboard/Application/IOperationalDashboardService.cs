using MotoSOS.API.Modules.OperationalDashboard.Contracts;

namespace MotoSOS.API.Modules.OperationalDashboard.Application;

public interface IOperationalDashboardService
{
    Task<OperationalDashboardSummaryResponse> GetSummaryAsync(string adminUserId, CancellationToken cancellationToken);
    Task<OperationalDashboardIncidentListResponse> ListIncidentsAsync(string adminUserId, OperationalDashboardQuery query, CancellationToken cancellationToken);
    Task<OperationalDashboardResponseTimesResponse> GetResponseTimesAsync(string adminUserId, OperationalDashboardQuery query, CancellationToken cancellationToken);
    Task<OperationalDashboardResolutionOutcomesResponse> GetResolutionOutcomesAsync(string adminUserId, OperationalDashboardQuery query, CancellationToken cancellationToken);
    Task<OperationalDashboardOfflineProcessingSummaryResponse> GetOfflineProcessingAsync(string adminUserId, CancellationToken cancellationToken);
}
