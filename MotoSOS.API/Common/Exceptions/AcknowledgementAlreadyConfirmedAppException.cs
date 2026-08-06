namespace MotoSOS.API.Common.Exceptions;

public sealed class AcknowledgementAlreadyConfirmedAppException : AppException
{
    public AcknowledgementAlreadyConfirmedAppException(string message) : base(message, StatusCodes.Status400BadRequest, "acknowledgement_already_confirmed") { }
}
