namespace MotoSOS.API.Common.Exceptions;

public sealed class EmergencyResolutionNotAvailableAppException : AppException
{
    public EmergencyResolutionNotAvailableAppException(string message) : base(message, StatusCodes.Status404NotFound, "emergency_resolution_not_available") { }
}
