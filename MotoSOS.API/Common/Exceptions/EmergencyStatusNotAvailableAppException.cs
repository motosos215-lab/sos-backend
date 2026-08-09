namespace MotoSOS.API.Common.Exceptions;

public sealed class EmergencyStatusNotAvailableAppException : AppException
{
    public EmergencyStatusNotAvailableAppException(string message) : base(message, StatusCodes.Status404NotFound, "emergency_status_not_available") { }
}
