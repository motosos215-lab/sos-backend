namespace MotoSOS.API.Common.Exceptions;

public sealed class ActiveTripExistsAppException : AppException
{
    public ActiveTripExistsAppException(string message)
        : base(message, StatusCodes.Status409Conflict, "active_trip_exists")
    {
    }
}
