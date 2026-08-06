namespace MotoSOS.API.Common.Exceptions;

public sealed class AlertDispatchAlreadyCompletedAppException : AppException
{
    public AlertDispatchAlreadyCompletedAppException(string message) : base(message, StatusCodes.Status400BadRequest, "alert_dispatch_already_completed") { }
}
