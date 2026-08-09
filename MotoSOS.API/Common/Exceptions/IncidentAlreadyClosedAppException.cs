namespace MotoSOS.API.Common.Exceptions;

public sealed class IncidentAlreadyClosedAppException : AppException
{
    public IncidentAlreadyClosedAppException(string message)
        : base(message, StatusCodes.Status400BadRequest, "incident_already_closed")
    {
    }
}
