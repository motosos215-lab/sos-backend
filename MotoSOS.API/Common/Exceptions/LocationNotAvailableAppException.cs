namespace MotoSOS.API.Common.Exceptions;

public sealed class LocationNotAvailableAppException : AppException
{
    public LocationNotAvailableAppException(string message) : base(message, StatusCodes.Status404NotFound, "location_not_available") { }
}
