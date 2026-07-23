namespace MotoSOS.API.Common.Exceptions;

public class ApiException : AppException
{
    public ApiException(string message)
        : base(message, StatusCodes.Status500InternalServerError, "api_error")
    {
    }
}
