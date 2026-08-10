namespace MotoSOS.API.Modules.Escalations.Application;

public interface IEmergencyEscalationIdempotencyKeyFactory
{
    string Create(string userId, string alertDispatchId);
}
