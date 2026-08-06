namespace MotoSOS.API.Common.Exceptions;

public sealed class NotificationNotAllowedAppException : AppException
{
    public NotificationNotAllowedAppException(string message) : base(message, StatusCodes.Status400BadRequest, "notification_not_allowed") { }
}
