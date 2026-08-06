namespace MotoSOS.API.Common.Exceptions;

public sealed class ActivationCodeInvalidAppException : AppException
{
    public ActivationCodeInvalidAppException(string message)
        : base(message, StatusCodes.Status400BadRequest, "activation_code_invalid")
    {
    }
}
