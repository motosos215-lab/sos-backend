namespace MotoSOS.API.Common.Exceptions;

public sealed class ValidationAppException : AppException
{
    public ValidationAppException(string message)
        : base(message, StatusCodes.Status400BadRequest, "validation_error")
    {
    }
}
