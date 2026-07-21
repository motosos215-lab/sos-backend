namespace MotoSOS.API.Common.Exceptions;

public sealed class ForbiddenAppException : AppException
{
    public ForbiddenAppException(string message)
        : base(message, StatusCodes.Status403Forbidden, "forbidden")
    {
    }
}
