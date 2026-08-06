namespace MotoSOS.API.Common.Exceptions;

public sealed class AcknowledgementNotAllowedAppException : AppException
{
    public AcknowledgementNotAllowedAppException(string message) : base(message, StatusCodes.Status400BadRequest, "acknowledgement_not_allowed") { }
}
