using MotoSOS.API.Modules.EmergencyResolution.Application;
using MotoSOS.API.Modules.EmergencyResolution.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class UnconfiguredEmergencyResolutionRepository : IEmergencyResolutionRepository
{
    private static InvalidOperationException CreateException() => new("MongoDB is not configured. Configure MongoDB settings to use Emergency Resolution API.");
    public Task<EmergencyResolutionReport?> GetByIncidentIdAsync(string incidentId, CancellationToken cancellationToken) => throw CreateException();
    public Task<EmergencyResolutionReport?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) => throw CreateException();
    public Task<(EmergencyResolutionReport Report, bool IsDuplicate)> AddOrGetDuplicateAsync(EmergencyResolutionReport report, CancellationToken cancellationToken) => throw CreateException();
    public Task<IReadOnlyList<EmergencyResolutionReport>> ListByUserIdAsync(string userId, EmergencyResolutionOutcome? outcome, int pageNumber, int pageSize, CancellationToken cancellationToken) => throw CreateException();
    public Task<long> CountByUserIdAsync(string userId, EmergencyResolutionOutcome? outcome, CancellationToken cancellationToken) => throw CreateException();
}
