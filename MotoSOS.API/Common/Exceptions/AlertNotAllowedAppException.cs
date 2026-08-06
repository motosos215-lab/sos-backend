namespace MotoSOS.API.Common.Exceptions;

public sealed class AlertNotAllowedAppException : AppException
{
    public AlertNotAllowedAppException(string message) : base(message, StatusCodes.Status400BadRequest, "alert_not_allowed") { }
}
