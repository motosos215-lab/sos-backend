namespace MotoSOS.API.Modules.EvidenceAttachments.Contracts;

public sealed record UploadEvidenceAttachmentResponse(EvidenceAttachmentResponse EvidenceAttachment, bool IsDuplicate);
