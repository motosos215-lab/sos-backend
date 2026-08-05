namespace MotoSOS.API.Common.Exceptions;

public sealed class PlanLimitExceededAppException : AppException
{
    public PlanLimitExceededAppException(string message)
        : base(message, StatusCodes.Status409Conflict, "plan_limit_exceeded")
    {
    }
}
