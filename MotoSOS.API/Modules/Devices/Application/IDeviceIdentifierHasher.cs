namespace MotoSOS.API.Modules.Devices.Application;

public interface IDeviceIdentifierHasher
{
    string? Hash(string? deviceIdentifier);
}
