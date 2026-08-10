using System.Security.Cryptography;
using System.Text;

namespace MotoSOS.API.Modules.EvidenceAttachments.Application;

public sealed class EvidenceAttachmentIdempotencyKeyFactory : IEvidenceAttachmentIdempotencyKeyFactory
{
    public string Create(string ownerUserId, string clientEvidenceId, string targetType, string targetId)
    {
        string input = string.Join('|', ownerUserId.Trim(), clientEvidenceId.Trim(), targetType.Trim(), targetId.Trim());
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
