namespace MotoSOS.API.Common.Exceptions;

public sealed class TripNotReadyAppException : AppException
{
    public TripNotReadyAppException(string message)
        : base(message, StatusCodes.Status400BadRequest, "trip_not_ready")
    {
    }
}
