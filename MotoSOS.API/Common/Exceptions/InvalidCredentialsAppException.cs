namespace MotoSOS.API.Common.Exceptions;

public sealed class InvalidCredentialsAppException : AppException
{
    public InvalidCredentialsAppException()
        : base("Invalid authentication credentials.", StatusCodes.Status401Unauthorized, "invalid_credentials")
    {
    }
}
