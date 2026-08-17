namespace MotoSOS.API.Common.Exceptions;

public sealed class EvidenceStorageAppException : AppException
{
    public EvidenceStorageAppException(string message, string code, int statusCode = StatusCodes.Status400BadRequest) : base(message, statusCode, code) { }
}
