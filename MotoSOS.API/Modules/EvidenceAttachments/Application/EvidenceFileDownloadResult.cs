namespace MotoSOS.API.Modules.EvidenceAttachments.Application;

public sealed record EvidenceFileDownloadResult(Stream Content, string ContentType, long SizeBytes);
