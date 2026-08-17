namespace MotoSOS.API.Modules.EvidenceAttachments.Application;

public sealed class EvidenceStorageOptionsValidator
{
    public EvidenceStorageConfigurationStatus Validate(EvidenceStorageOptions options)
    {
        if (!options.Enabled) return new EvidenceStorageConfigurationStatus(false, false, NormalizeProvider(options.Provider), ["evidence_storage_disabled"]);
        var warnings = new List<string>();
        string provider = NormalizeProvider(options.Provider);
        if (!string.Equals(provider, "DigitalOceanSpaces", StringComparison.OrdinalIgnoreCase)) warnings.Add("evidence_storage_provider_not_supported");
        if (string.IsNullOrWhiteSpace(options.Bucket)) warnings.Add("evidence_storage_bucket_missing");
        if (string.IsNullOrWhiteSpace(options.Region)) warnings.Add("evidence_storage_region_missing");
        if (string.IsNullOrWhiteSpace(options.ServiceUrl) || !Uri.TryCreate(options.ServiceUrl, UriKind.Absolute, out _)) warnings.Add("evidence_storage_service_url_invalid");
        if (string.IsNullOrWhiteSpace(options.AccessKey)) warnings.Add("evidence_storage_access_key_missing");
        if (string.IsNullOrWhiteSpace(options.SecretKey)) warnings.Add("evidence_storage_secret_key_missing");
        if (string.IsNullOrWhiteSpace(options.BasePath)) warnings.Add("evidence_storage_base_path_missing");
        if (options.MaxFileSizeBytes <= 0) warnings.Add("evidence_storage_max_file_size_invalid");
        return new EvidenceStorageConfigurationStatus(options.Enabled, warnings.Count == 0, provider, warnings);
    }

    private static string NormalizeProvider(string? provider) => string.IsNullOrWhiteSpace(provider) ? "none" : provider.Trim();
}

public sealed record EvidenceStorageConfigurationStatus(bool Enabled, bool Configured, string ProviderName, IReadOnlyList<string> Warnings);
