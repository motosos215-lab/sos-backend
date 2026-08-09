namespace MotoSOS.API.Common.Exceptions;

public sealed class PushNotificationTokenNotAvailableAppException : AppException
{
    public PushNotificationTokenNotAvailableAppException(string message) : base(message, StatusCodes.Status404NotFound, "push_notification_token_not_available") { }
}
