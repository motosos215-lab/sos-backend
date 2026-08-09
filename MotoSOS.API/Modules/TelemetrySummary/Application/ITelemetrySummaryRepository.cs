using MotoSOS.API.Modules.TelemetrySummary.Domain;

namespace MotoSOS.API.Modules.TelemetrySummary.Application;

public interface ITelemetrySummaryRepository
{
    Task<TripTelemetrySummary?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task<TripTelemetrySummary?> GetByTripIdAsync(string tripId, CancellationToken cancellationToken);
    Task<TripTelemetrySummary?> GetByUserIdAndTripIdAsync(string userId, string tripId, CancellationToken cancellationToken);
    Task<TripTelemetrySummary> UpsertAsync(TripTelemetrySummary summary, CancellationToken cancellationToken);
    Task<IReadOnlyList<TripTelemetrySummary>> ListByUserIdAsync(string userId, TelemetrySummaryQuery query, CancellationToken cancellationToken);
    Task<long> CountByUserIdAsync(string userId, TelemetrySummaryQuery query, CancellationToken cancellationToken);
    Task<IReadOnlyList<TripTelemetrySummary>> ListAsync(TelemetrySummaryQuery query, CancellationToken cancellationToken);
    Task<long> CountAsync(TelemetrySummaryQuery query, CancellationToken cancellationToken);
}
