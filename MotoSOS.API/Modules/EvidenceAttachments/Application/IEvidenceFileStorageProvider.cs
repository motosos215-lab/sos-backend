namespace MotoSOS.API.Modules.EvidenceAttachments.Application;

public interface IEvidenceFileStorageProvider
{
    Task<EvidenceFileUploadResult> UploadAsync(EvidenceFileUploadRequest request, CancellationToken cancellationToken);
    Task<EvidenceFileDownloadResult> DownloadAsync(string objectKey, string contentType, CancellationToken cancellationToken);
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken);
}
