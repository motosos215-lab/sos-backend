namespace MotoSOS.API.Modules.EvidenceAttachments.Application;

public sealed class EvidenceStorageOptions
{
    public const string SectionName = "EvidenceStorage";
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "DigitalOceanSpaces";
    public string Bucket { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string ServiceUrl { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BasePath { get; set; } = "evidence";
    public bool UsePathStyle { get; set; }
    public long MaxFileSizeBytes { get; set; } = 10L * 1024L * 1024L;
}
