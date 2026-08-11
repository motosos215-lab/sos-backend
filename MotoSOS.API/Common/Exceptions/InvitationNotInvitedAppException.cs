namespace MotoSOS.API.Common.Exceptions;

public sealed class InvitationNotInvitedAppException : AppException
{
    public InvitationNotInvitedAppException(string message) : base(message, StatusCodes.Status400BadRequest, "invitation_not_invited") { }
}
