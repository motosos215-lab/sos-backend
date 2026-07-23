namespace MotoSOS.API.Common.Exceptions;

public sealed class UserAlreadyExistsAppException : AppException
{
    public UserAlreadyExistsAppException()
        : base("User registration could not be completed.", StatusCodes.Status409Conflict, "user_already_exists")
    {
    }
}
