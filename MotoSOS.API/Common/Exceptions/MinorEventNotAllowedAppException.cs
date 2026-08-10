namespace MotoSOS.API.Common.Exceptions;

public sealed class MinorEventNotAllowedAppException : AppException
{
    public MinorEventNotAllowedAppException(string message) : base(message, StatusCodes.Status400BadRequest, "minor_event_not_allowed") { }
}
