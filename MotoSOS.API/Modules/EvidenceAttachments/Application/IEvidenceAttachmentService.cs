using MotoSOS.API.Modules.EvidenceAttachments.Contracts;

namespace MotoSOS.API.Modules.EvidenceAttachments.Application;

public interface IEvidenceAttachmentService
{
    Task<CreateEvidenceAttachmentResponse> CreateForRiderAsync(string userId, CreateEvidenceAttachmentRequest request, CancellationToken cancellationToken);
    Task<CreateEvidenceAttachmentResponse> CreateForMonitorAsync(string userId, CreateEvidenceAttachmentRequest request, CancellationToken cancellationToken);
    Task<UploadEvidenceAttachmentResponse> UploadForRiderAsync(string userId, EvidenceUploadCommand command, CancellationToken cancellationToken);
    Task<UploadEvidenceAttachmentResponse> UploadForMonitorAsync(string userId, EvidenceUploadCommand command, CancellationToken cancellationToken);
    Task<GetEvidenceAttachmentsResponse> ListForRiderAsync(string userId, EvidenceAttachmentQuery query, CancellationToken cancellationToken);
    Task<EvidenceAttachmentResponse> GetForRiderAsync(string userId, string id, CancellationToken cancellationToken);
    Task<EvidenceAttachmentDownload> DownloadForRiderAsync(string userId, string id, CancellationToken cancellationToken);
    Task<EvidenceAttachmentDownload> DownloadForMonitorAsync(string userId, string id, CancellationToken cancellationToken);
    Task<EvidenceAttachmentResponse> DeleteForRiderAsync(string userId, string id, CancellationToken cancellationToken);
    Task<GetEvidenceAttachmentsResponse> ListForAdminAsync(string userId, EvidenceAttachmentQuery query, CancellationToken cancellationToken);
    Task<EvidenceAttachmentResponse> GetForAdminAsync(string userId, string id, CancellationToken cancellationToken);
    Task<EvidenceAttachmentDownload> DownloadForAdminAsync(string userId, string id, CancellationToken cancellationToken);
}

public sealed record EvidenceUploadCommand(string IncidentId, Stream Content, string FileName, string ContentType, long SizeBytes, string? Description, string? EvidenceType, string? ClientEvidenceId);
