namespace MotoSOS.API.Common.Exceptions;

public sealed class EmergencyEscalationNotAvailableAppException : AppException
{
    public EmergencyEscalationNotAvailableAppException(string message) : base(message, StatusCodes.Status404NotFound, "emergency_escalation_not_available") { }
}
