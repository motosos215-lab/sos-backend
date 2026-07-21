namespace MotoSOS.API.Common.Exceptions;

public sealed class UnauthorizedAppException : AppException
{
    public UnauthorizedAppException(string message)
        : base(message, StatusCodes.Status401Unauthorized, "unauthorized")
    {
    }
}
