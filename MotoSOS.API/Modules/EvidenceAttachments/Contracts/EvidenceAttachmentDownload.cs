namespace MotoSOS.API.Modules.EvidenceAttachments.Contracts;

public sealed record EvidenceAttachmentDownload(Stream Content, string ContentType, string FileName, long SizeBytes);
