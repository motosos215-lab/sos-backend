namespace MotoSOS.API.Modules.Notifications.Providers;

public sealed class FcmNotificationProviderOptionsValidator
{
    public FcmProviderConfigurationStatus Validate(FcmNotificationProviderOptions options)
    {
        if (!options.Enabled) return new FcmProviderConfigurationStatus(false, false, false, "none", ["fcm_disabled"]);

        var warnings = new List<string>();
        bool projectConfigured = !string.IsNullOrWhiteSpace(options.ProjectId);
        bool jsonConfigured = !string.IsNullOrWhiteSpace(options.ServiceAccountJson);
        bool jsonBase64Configured = !string.IsNullOrWhiteSpace(options.ServiceAccountJsonBase64);
        bool fileConfigured = !string.IsNullOrWhiteSpace(options.ServiceAccountFilePath);
        int credentialSources = Convert.ToInt32(jsonConfigured) + Convert.ToInt32(jsonBase64Configured) + Convert.ToInt32(fileConfigured);
        if (!projectConfigured) warnings.Add("project_id_missing");
        if (credentialSources == 0) warnings.Add("credentials_missing");
        if (credentialSources > 1) warnings.Add("multiple_credential_sources_configured");
        return new FcmProviderConfigurationStatus(options.Enabled, projectConfigured && credentialSources > 0, projectConfigured, GetCredentialSource(jsonConfigured, jsonBase64Configured, fileConfigured), warnings);
    }

    private static string GetCredentialSource(bool jsonConfigured, bool jsonBase64Configured, bool fileConfigured)
    {
        if (jsonConfigured) return "environment_json";
        if (jsonBase64Configured) return "environment_json_base64";
        return fileConfigured ? "environment_file_path" : "none";
    }
}

public sealed record FcmProviderConfigurationStatus(bool Enabled, bool Configured, bool ProjectIdConfigured, string CredentialSource, IReadOnlyList<string> Warnings);
