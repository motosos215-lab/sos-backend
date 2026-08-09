namespace MotoSOS.API.Modules.Incidents.Application;

public interface IIncidentIdempotencyKeyFactory
{
    string Create(string userId, string tripId, string clientIncidentId);
}
