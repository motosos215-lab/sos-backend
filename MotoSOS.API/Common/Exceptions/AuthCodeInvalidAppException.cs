namespace MotoSOS.API.Common.Exceptions;

public sealed class AuthCodeInvalidAppException : AppException
{
    public AuthCodeInvalidAppException()
        : base("The code is invalid or expired.", StatusCodes.Status400BadRequest, "invalid_or_expired_code")
    {
    }
}
