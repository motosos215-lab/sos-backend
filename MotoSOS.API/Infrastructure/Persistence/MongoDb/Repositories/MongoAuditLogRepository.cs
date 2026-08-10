using MongoDB.Driver;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Collections;
using MotoSOS.API.Modules.AuditLogs.Application;
using MotoSOS.API.Modules.AuditLogs.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class MongoAuditLogRepository : IAuditLogRepository
{
    private readonly IMongoCollection<AuditLogEntry> _auditLogs;

    public MongoAuditLogRepository(IMongoDatabase database) => _auditLogs = database.GetCollection<AuditLogEntry>(MongoCollectionNames.AuditLogs);

    public Task AddAsync(AuditLogEntry entry, CancellationToken cancellationToken) => _auditLogs.InsertOneAsync(entry, cancellationToken: cancellationToken);

    public async Task<AuditLogEntry?> GetByIdAsync(string id, CancellationToken cancellationToken) => await _auditLogs.Find(log => log.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<AuditLogEntry>> ListAsync(AuditLogQuery query, CancellationToken cancellationToken) => await _auditLogs
        .Find(BuildFilter(query))
        .SortByDescending(log => log.CreatedAtUtc)
        .Skip((query.PageNumber - 1) * query.PageSize)
        .Limit(query.PageSize)
        .ToListAsync(cancellationToken);

    public async Task<long> CountAsync(AuditLogQuery query, CancellationToken cancellationToken) => await _auditLogs.CountDocumentsAsync(BuildFilter(query), cancellationToken: cancellationToken);

    public async Task<long> CountOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken) => await _auditLogs.CountDocumentsAsync(Builders<AuditLogEntry>.Filter.Lt(log => log.CreatedAtUtc, cutoffUtc), cancellationToken: cancellationToken);

    public async Task<long> DeleteOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken)
    {
        DeleteResult result = await _auditLogs.DeleteManyAsync(Builders<AuditLogEntry>.Filter.Lt(log => log.CreatedAtUtc, cutoffUtc), cancellationToken);
        return result.DeletedCount;
    }

    private static FilterDefinition<AuditLogEntry> BuildFilter(AuditLogQuery query)
    {
        FilterDefinitionBuilder<AuditLogEntry> b = Builders<AuditLogEntry>.Filter;
        FilterDefinition<AuditLogEntry> f = b.Empty;
        if (!string.IsNullOrWhiteSpace(query.ActorUserId)) f &= b.Eq(log => log.ActorUserId, query.ActorUserId);
        if (query.Action.HasValue) f &= b.Eq(log => log.Action, query.Action.Value);
        if (query.Module.HasValue) f &= b.Eq(log => log.Module, query.Module.Value);
        if (query.Outcome.HasValue) f &= b.Eq(log => log.Outcome, query.Outcome.Value);
        if (!string.IsNullOrWhiteSpace(query.EntityType)) f &= b.Eq(log => log.EntityType, query.EntityType);
        if (!string.IsNullOrWhiteSpace(query.EntityId)) f &= b.Eq(log => log.EntityId, query.EntityId);
        if (query.DateFrom.HasValue) f &= b.Gte(log => log.CreatedAtUtc, query.DateFrom.Value);
        if (query.DateTo.HasValue) f &= b.Lte(log => log.CreatedAtUtc, query.DateTo.Value);
        return f;
    }
}
