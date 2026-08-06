namespace MotoSOS.API.Common.Exceptions;

public sealed class OnboardingNotReadyAppException : AppException
{
    public OnboardingNotReadyAppException(string message)
        : base(message, StatusCodes.Status400BadRequest, "onboarding_not_ready")
    {
    }
}
