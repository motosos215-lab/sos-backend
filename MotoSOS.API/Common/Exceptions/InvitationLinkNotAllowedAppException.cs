namespace MotoSOS.API.Common.Exceptions;

public sealed class InvitationLinkNotAllowedAppException : AppException
{
    public InvitationLinkNotAllowedAppException(string message) : base(message, StatusCodes.Status400BadRequest, "invitation_link_not_allowed") { }
}
