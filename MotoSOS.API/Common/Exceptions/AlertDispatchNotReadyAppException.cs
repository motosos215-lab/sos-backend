namespace MotoSOS.API.Common.Exceptions;

public sealed class AlertDispatchNotReadyAppException : AppException
{
    public AlertDispatchNotReadyAppException(string message) : base(message, StatusCodes.Status400BadRequest, "alert_dispatch_not_ready") { }
}
