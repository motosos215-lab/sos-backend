namespace MotoSOS.API.Common.Exceptions;

public sealed class PushNotificationTokenNotAllowedAppException : AppException
{
    public PushNotificationTokenNotAllowedAppException(string message) : base(message, StatusCodes.Status400BadRequest, "push_notification_token_not_allowed") { }
}
