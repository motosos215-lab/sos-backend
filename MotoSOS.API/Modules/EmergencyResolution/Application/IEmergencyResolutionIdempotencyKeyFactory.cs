namespace MotoSOS.API.Modules.EmergencyResolution.Application;

public interface IEmergencyResolutionIdempotencyKeyFactory
{
    string Create(string userId, string incidentId);
}
