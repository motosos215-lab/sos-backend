namespace MotoSOS.API.Common.Exceptions;

public sealed class MinorEventNotAvailableAppException : AppException
{
    public MinorEventNotAvailableAppException(string message) : base(message, StatusCodes.Status404NotFound, "minor_event_not_available") { }
}
