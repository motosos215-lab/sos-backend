using MotoSOS.API.Modules.EvidenceAttachments.Domain;

namespace MotoSOS.API.Modules.EvidenceAttachments.Application;

public sealed record EvidenceAttachmentQuery(
    string? UserId,
    string? IncidentId,
    string? AlertDispatchId,
    string? EmergencyResolutionReportId,
    EvidenceType? EvidenceType,
    EvidenceSource? Source,
    EvidenceAttachmentStatus? Status,
    DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo,
    int PageNumber,
    int PageSize);
