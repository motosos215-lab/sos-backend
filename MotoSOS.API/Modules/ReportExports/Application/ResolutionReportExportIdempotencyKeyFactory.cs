using System.Security.Cryptography;
using System.Text;

namespace MotoSOS.API.Modules.ReportExports.Application;

public sealed class ResolutionReportExportIdempotencyKeyFactory : IResolutionReportExportIdempotencyKeyFactory
{
    public string Create(string userId, string emergencyResolutionReportId, string exportType)
    {
        string input = string.Join('|', userId.Trim(), emergencyResolutionReportId.Trim(), exportType.Trim());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }
}
