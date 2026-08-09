namespace MotoSOS.API.Common.Exceptions;

public sealed class EvidenceAttachmentNotAvailableAppException : AppException
{
    public EvidenceAttachmentNotAvailableAppException(string message) : base(message, StatusCodes.Status404NotFound, "evidence_attachment_not_available") { }
}
