namespace MotoSOS.API.Common.Exceptions;

public sealed class EvidenceUploadConflictAppException : AppException
{
    public EvidenceUploadConflictAppException(string message) : base(message, StatusCodes.Status409Conflict, "evidence_upload_conflict") { }
}
