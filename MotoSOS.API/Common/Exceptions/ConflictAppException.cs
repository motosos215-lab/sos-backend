namespace MotoSOS.API.Common.Exceptions;

public sealed class ConflictAppException : AppException
{
    public ConflictAppException(string message)
        : base(message, StatusCodes.Status409Conflict, "conflict")
    {
    }
}
