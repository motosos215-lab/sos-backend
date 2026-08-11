namespace MotoSOS.API.Common.Exceptions;

public sealed class InvitationExpiredAppException : AppException
{
    public InvitationExpiredAppException(string message) : base(message, StatusCodes.Status400BadRequest, "invitation_expired") { }
}
