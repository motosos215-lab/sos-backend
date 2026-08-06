using System.Security.Cryptography;
using System.Text;

namespace MotoSOS.API.Modules.Devices.Application;

public sealed class DeviceIdentifierHasher : IDeviceIdentifierHasher
{
    public string? Hash(string? deviceIdentifier)
    {
        if (string.IsNullOrWhiteSpace(deviceIdentifier))
        {
            return null;
        }

        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(deviceIdentifier.Trim()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
