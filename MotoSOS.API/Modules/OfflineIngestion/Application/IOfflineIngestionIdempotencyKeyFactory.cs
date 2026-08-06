namespace MotoSOS.API.Modules.OfflineIngestion.Application;

public interface IOfflineIngestionIdempotencyKeyFactory
{
    string Create(string userId, string mobileDeviceId, string tripId, string type, string clientEventId, int payloadVersion);
}
