namespace MotoSOS.API.Common.Exceptions;

public sealed class InvitationNotFoundAppException : AppException
{
    public InvitationNotFoundAppException(string message) : base(message, StatusCodes.Status404NotFound, "invitation_not_found") { }
}
