using MongoDB.Driver;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Collections;
using MotoSOS.API.Modules.AuditLogRetention.Application;
using MotoSOS.API.Modules.AuditLogRetention.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class MongoAuditLogRetentionRunRepository : IAuditLogRetentionRunRepository
{
    private readonly IMongoCollection<AuditLogRetentionRun> _runs;

    public MongoAuditLogRetentionRunRepository(IMongoDatabase database) => _runs = database.GetCollection<AuditLogRetentionRun>(MongoCollectionNames.AuditLogRetentionRuns);

    public Task AddAsync(AuditLogRetentionRun run, CancellationToken cancellationToken) => _runs.InsertOneAsync(run, cancellationToken: cancellationToken);

    public async Task<AuditLogRetentionRun?> GetByIdAsync(string id, CancellationToken cancellationToken) => await _runs.Find(run => run.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<AuditLogRetentionRun>> ListAsync(AuditLogRetentionRunQuery query, CancellationToken cancellationToken) => await _runs
        .Find(Builders<AuditLogRetentionRun>.Filter.Empty)
        .SortByDescending(run => run.CreatedAtUtc)
        .Skip((query.PageNumber - 1) * query.PageSize)
        .Limit(query.PageSize)
        .ToListAsync(cancellationToken);

    public async Task<long> CountAsync(CancellationToken cancellationToken) => await _runs.CountDocumentsAsync(Builders<AuditLogRetentionRun>.Filter.Empty, cancellationToken: cancellationToken);
}
