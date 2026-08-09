namespace MotoSOS.API.Modules.Notifications.Providers;

public sealed class FcmNotificationProviderOptionsValidator
{
    public FcmProviderConfigurationStatus Validate(FcmNotificationProviderOptions options)
    {
        if (!options.Enabled) return new FcmProviderConfigurationStatus(false, false, false, "none", ["fcm_disabled"]);

        var warnings = new List<string>();
        bool projectConfigured = !string.IsNullOrWhiteSpace(options.ProjectId);
        bool jsonConfigured = !string.IsNullOrWhiteSpace(options.ServiceAccountJson);
        bool fileConfigured = !string.IsNullOrWhiteSpace(options.ServiceAccountFilePath);
        if (!projectConfigured) warnings.Add("project_id_missing");
        if (!jsonConfigured && !fileConfigured) warnings.Add("credentials_missing");
        if (jsonConfigured && fileConfigured) warnings.Add("multiple_credential_sources_configured");
        return new FcmProviderConfigurationStatus(options.Enabled, projectConfigured && (jsonConfigured || fileConfigured) && !(jsonConfigured && fileConfigured), projectConfigured, jsonConfigured ? "environment_json" : fileConfigured ? "environment_file_path" : "none", warnings);
    }
}

public sealed record FcmProviderConfigurationStatus(bool Enabled, bool Configured, bool ProjectIdConfigured, string CredentialSource, IReadOnlyList<string> Warnings);
