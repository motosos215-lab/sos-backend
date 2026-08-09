using MotoSOS.API.Modules.EvidenceAttachments.Application;
using MotoSOS.API.Modules.EvidenceAttachments.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class UnconfiguredEvidenceAttachmentRepository : IEvidenceAttachmentRepository
{
    private static InvalidOperationException CreateException() => new("MongoDB is not configured. Configure MongoDB settings to use Evidence Attachments API.");
    public Task<EvidenceAttachment?> GetByIdAsync(string id, CancellationToken cancellationToken) => throw CreateException();
    public Task<EvidenceAttachment?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) => throw CreateException();
    public Task<(EvidenceAttachment EvidenceAttachment, bool IsDuplicate)> AddOrGetDuplicateAsync(EvidenceAttachment evidenceAttachment, CancellationToken cancellationToken) => throw CreateException();
    public Task UpdateAsync(EvidenceAttachment evidenceAttachment, CancellationToken cancellationToken) => throw CreateException();
    public Task<IReadOnlyList<EvidenceAttachment>> ListByUserIdAsync(string userId, EvidenceAttachmentQuery query, CancellationToken cancellationToken) => throw CreateException();
    public Task<long> CountByUserIdAsync(string userId, EvidenceAttachmentQuery query, CancellationToken cancellationToken) => throw CreateException();
    public Task<IReadOnlyList<EvidenceAttachment>> ListAsync(EvidenceAttachmentQuery query, CancellationToken cancellationToken) => throw CreateException();
    public Task<long> CountAsync(EvidenceAttachmentQuery query, CancellationToken cancellationToken) => throw CreateException();
}
