using MongoDB.Driver;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Collections;
using MotoSOS.API.Modules.EmergencyResolution.Application;
using MotoSOS.API.Modules.EmergencyResolution.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class MongoEmergencyResolutionRepository : IEmergencyResolutionRepository
{
    private readonly IMongoCollection<EmergencyResolutionReport> _reports;
    public MongoEmergencyResolutionRepository(IMongoDatabase database) => _reports = database.GetCollection<EmergencyResolutionReport>(MongoCollectionNames.EmergencyResolutionReports);
    public async Task<EmergencyResolutionReport?> GetByIncidentIdAsync(string incidentId, CancellationToken cancellationToken) => await _reports.Find(r => r.IncidentId == incidentId).FirstOrDefaultAsync(cancellationToken);
    public async Task<EmergencyResolutionReport?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) => await _reports.Find(r => r.IdempotencyKey == idempotencyKey).FirstOrDefaultAsync(cancellationToken);
    public async Task<(EmergencyResolutionReport Report, bool IsDuplicate)> AddOrGetDuplicateAsync(EmergencyResolutionReport report, CancellationToken cancellationToken)
    {
        EmergencyResolutionReport? existing = await GetByIdempotencyKeyAsync(report.IdempotencyKey, cancellationToken) ?? await GetByIncidentIdAsync(report.IncidentId, cancellationToken);
        if (existing is not null) return (existing, true);
        try { await _reports.InsertOneAsync(report, cancellationToken: cancellationToken); return (report, false); }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            existing = await GetByIdempotencyKeyAsync(report.IdempotencyKey, cancellationToken) ?? await GetByIncidentIdAsync(report.IncidentId, cancellationToken);
            if (existing is not null) return (existing, true);
            throw;
        }
    }
    public async Task<IReadOnlyList<EmergencyResolutionReport>> ListByUserIdAsync(string userId, EmergencyResolutionOutcome? outcome, int pageNumber, int pageSize, CancellationToken cancellationToken) => await _reports.Find(BuildFilter(userId, outcome)).SortByDescending(r => r.CreatedAtUtc).Skip((pageNumber - 1) * pageSize).Limit(pageSize).ToListAsync(cancellationToken);
    public async Task<long> CountByUserIdAsync(string userId, EmergencyResolutionOutcome? outcome, CancellationToken cancellationToken) => await _reports.CountDocumentsAsync(BuildFilter(userId, outcome), cancellationToken: cancellationToken);
    private static FilterDefinition<EmergencyResolutionReport> BuildFilter(string userId, EmergencyResolutionOutcome? outcome) { var b = Builders<EmergencyResolutionReport>.Filter; var f = b.Eq(r => r.UserId, userId); if (outcome.HasValue) f &= b.Eq(r => r.Outcome, outcome.Value); return f; }
}
