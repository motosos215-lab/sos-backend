namespace MotoSOS.API.Modules.EvidenceAttachments.Application;

public sealed record EvidenceFileUploadResult(string ProviderName, string Bucket, string ObjectKey);
