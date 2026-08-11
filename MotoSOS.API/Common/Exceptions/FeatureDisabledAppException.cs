namespace MotoSOS.API.Common.Exceptions;

public sealed class FeatureDisabledAppException : AppException
{
    public FeatureDisabledAppException(string message)
        : base(message, StatusCodes.Status400BadRequest, "feature_disabled")
    {
    }
}
