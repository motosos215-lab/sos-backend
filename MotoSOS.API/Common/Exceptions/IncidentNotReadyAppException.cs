namespace MotoSOS.API.Common.Exceptions;

public sealed class IncidentNotReadyAppException : AppException
{
    public IncidentNotReadyAppException(string message) : base(message, StatusCodes.Status400BadRequest, "incident_not_ready") { }
}
