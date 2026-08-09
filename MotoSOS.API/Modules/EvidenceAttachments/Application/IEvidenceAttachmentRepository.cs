using MotoSOS.API.Modules.EvidenceAttachments.Domain;

namespace MotoSOS.API.Modules.EvidenceAttachments.Application;

public interface IEvidenceAttachmentRepository
{
    Task<EvidenceAttachment?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task<EvidenceAttachment?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);
    Task<(EvidenceAttachment EvidenceAttachment, bool IsDuplicate)> AddOrGetDuplicateAsync(EvidenceAttachment evidenceAttachment, CancellationToken cancellationToken);
    Task UpdateAsync(EvidenceAttachment evidenceAttachment, CancellationToken cancellationToken);
    Task<IReadOnlyList<EvidenceAttachment>> ListByUserIdAsync(string userId, EvidenceAttachmentQuery query, CancellationToken cancellationToken);
    Task<long> CountByUserIdAsync(string userId, EvidenceAttachmentQuery query, CancellationToken cancellationToken);
    Task<IReadOnlyList<EvidenceAttachment>> ListAsync(EvidenceAttachmentQuery query, CancellationToken cancellationToken);
    Task<long> CountAsync(EvidenceAttachmentQuery query, CancellationToken cancellationToken);
}
