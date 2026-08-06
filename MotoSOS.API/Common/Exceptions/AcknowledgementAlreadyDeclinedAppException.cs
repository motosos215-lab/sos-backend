namespace MotoSOS.API.Common.Exceptions;

public sealed class AcknowledgementAlreadyDeclinedAppException : AppException
{
    public AcknowledgementAlreadyDeclinedAppException(string message) : base(message, StatusCodes.Status400BadRequest, "acknowledgement_already_declined") { }
}
