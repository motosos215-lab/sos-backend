namespace MotoSOS.API.Modules.EvidenceAttachments.Domain;

public enum EvidenceStorageProvider
{
    None = 0,
    ExternalReference = 1,
    FutureObjectStorage = 2,
    DigitalOceanSpaces = 3
}
