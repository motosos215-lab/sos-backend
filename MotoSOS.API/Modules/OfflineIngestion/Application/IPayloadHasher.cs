namespace MotoSOS.API.Modules.OfflineIngestion.Application;

public interface IPayloadHasher
{
    string Hash(string payloadJson);
}
