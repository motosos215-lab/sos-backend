namespace MotoSOS.API.Modules.MinorEvents.Application;

public interface IMinorEventIdempotencyKeyFactory
{
    string Create(string userId, string tripId, string clientEventId, string eventType);
}
