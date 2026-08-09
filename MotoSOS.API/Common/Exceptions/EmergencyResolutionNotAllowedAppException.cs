namespace MotoSOS.API.Common.Exceptions;

public sealed class EmergencyResolutionNotAllowedAppException : AppException
{
    public EmergencyResolutionNotAllowedAppException(string message) : base(message, StatusCodes.Status400BadRequest, "emergency_resolution_not_allowed") { }
}
