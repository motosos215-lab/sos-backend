namespace MotoSOS.API.Common.Exceptions;

public sealed class NotFoundAppException : AppException
{
    public NotFoundAppException(string message)
        : base(message, StatusCodes.Status404NotFound, "not_found")
    {
    }
}
