namespace MotoSOS.API.Common.Exceptions;

public sealed class TermsNotAcceptedAppException : AppException
{
    public TermsNotAcceptedAppException()
        : base("Terms and conditions must be accepted.", StatusCodes.Status400BadRequest, "terms_not_accepted")
    {
    }
}
