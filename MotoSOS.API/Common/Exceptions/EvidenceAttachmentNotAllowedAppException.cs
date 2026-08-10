namespace MotoSOS.API.Common.Exceptions;

public sealed class EvidenceAttachmentNotAllowedAppException : AppException
{
    public EvidenceAttachmentNotAllowedAppException(string message) : base(message, StatusCodes.Status400BadRequest, "evidence_attachment_not_allowed") { }
}
