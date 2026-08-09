using MotoSOS.API.Modules.TelemetrySummary.Contracts;

namespace MotoSOS.API.Modules.TelemetrySummary.Application;

public interface ITelemetrySummaryService
{
    Task<TelemetrySummaryResponse> GetForRiderAsync(string userId, string tripId, CancellationToken cancellationToken);
    Task<TelemetrySummaryResponse> RecomputeForRiderAsync(string userId, string tripId, CancellationToken cancellationToken);
    Task<GetTelemetrySummariesResponse> ListForAdminAsync(string adminUserId, TelemetrySummaryQuery query, CancellationToken cancellationToken);
    Task<TelemetrySummaryResponse> GetForAdminAsync(string adminUserId, string id, CancellationToken cancellationToken);
}
