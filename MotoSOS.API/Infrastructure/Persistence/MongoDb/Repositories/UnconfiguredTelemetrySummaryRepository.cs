using MotoSOS.API.Modules.TelemetrySummary.Application;
using MotoSOS.API.Modules.TelemetrySummary.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class UnconfiguredTelemetrySummaryRepository : ITelemetrySummaryRepository
{
    private static InvalidOperationException Unconfigured() => new("MongoDB is not configured.");
    public Task<TripTelemetrySummary?> GetByIdAsync(string id, CancellationToken cancellationToken) => throw Unconfigured();
    public Task<TripTelemetrySummary?> GetByTripIdAsync(string tripId, CancellationToken cancellationToken) => throw Unconfigured();
    public Task<TripTelemetrySummary?> GetByUserIdAndTripIdAsync(string userId, string tripId, CancellationToken cancellationToken) => throw Unconfigured();
    public Task<TripTelemetrySummary> UpsertAsync(TripTelemetrySummary summary, CancellationToken cancellationToken) => throw Unconfigured();
    public Task<IReadOnlyList<TripTelemetrySummary>> ListByUserIdAsync(string userId, TelemetrySummaryQuery query, CancellationToken cancellationToken) => throw Unconfigured();
    public Task<long> CountByUserIdAsync(string userId, TelemetrySummaryQuery query, CancellationToken cancellationToken) => throw Unconfigured();
    public Task<IReadOnlyList<TripTelemetrySummary>> ListAsync(TelemetrySummaryQuery query, CancellationToken cancellationToken) => throw Unconfigured();
    public Task<long> CountAsync(TelemetrySummaryQuery query, CancellationToken cancellationToken) => throw Unconfigured();
}
