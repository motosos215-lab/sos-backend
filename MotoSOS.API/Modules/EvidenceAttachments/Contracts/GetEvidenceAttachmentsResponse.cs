namespace MotoSOS.API.Modules.EvidenceAttachments.Contracts;

public sealed record GetEvidenceAttachmentsResponse(IReadOnlyList<EvidenceAttachmentResponse> EvidenceAttachments, int PageNumber, int PageSize, long TotalCount);
