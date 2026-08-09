namespace MotoSOS.API.Common.Exceptions;

public sealed class EmergencyEscalationNotAllowedAppException : AppException
{
    public EmergencyEscalationNotAllowedAppException(string message) : base(message, StatusCodes.Status400BadRequest, "emergency_escalation_not_allowed") { }
}
