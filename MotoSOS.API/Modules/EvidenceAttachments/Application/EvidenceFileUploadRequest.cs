namespace MotoSOS.API.Modules.EvidenceAttachments.Application;

public sealed record EvidenceFileUploadRequest(Stream Content, string ObjectKey, string ContentType, long SizeBytes);
