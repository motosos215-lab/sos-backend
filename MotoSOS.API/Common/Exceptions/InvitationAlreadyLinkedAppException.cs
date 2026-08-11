namespace MotoSOS.API.Common.Exceptions;

public sealed class InvitationAlreadyLinkedAppException : AppException
{
    public InvitationAlreadyLinkedAppException(string message) : base(message, StatusCodes.Status409Conflict, "invitation_already_linked") { }
}
