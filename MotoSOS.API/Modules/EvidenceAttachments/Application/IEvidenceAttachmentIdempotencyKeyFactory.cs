namespace MotoSOS.API.Modules.EvidenceAttachments.Application;

public interface IEvidenceAttachmentIdempotencyKeyFactory
{
    string Create(string ownerUserId, string clientEvidenceId, string targetType, string targetId);
}
