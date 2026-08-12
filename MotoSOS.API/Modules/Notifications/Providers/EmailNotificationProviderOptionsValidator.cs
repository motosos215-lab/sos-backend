namespace MotoSOS.API.Modules.Notifications.Providers;

public sealed class EmailNotificationProviderOptionsValidator
{
    public EmailProviderConfigurationStatus Validate(EmailNotificationProviderOptions options)
    {
        if (!options.Enabled) return new EmailProviderConfigurationStatus(false, false, "none", ["email_disabled"]);

        var warnings = new List<string>();
        bool fromConfigured = !string.IsNullOrWhiteSpace(options.FromEmail);
        bool hostConfigured = !string.IsNullOrWhiteSpace(options.SmtpHost);
        bool usernameConfigured = !string.IsNullOrWhiteSpace(options.SmtpUsername);
        bool passwordConfigured = !string.IsNullOrWhiteSpace(options.SmtpPassword);
        bool portConfigured = options.SmtpPort > 0;

        if (!fromConfigured) warnings.Add("from_email_missing");
        if (!hostConfigured) warnings.Add("smtp_host_missing");
        if (!portConfigured) warnings.Add("smtp_port_invalid");
        if (!usernameConfigured) warnings.Add("smtp_username_missing");
        if (!passwordConfigured) warnings.Add("smtp_password_missing");

        return new EmailProviderConfigurationStatus(options.Enabled, fromConfigured && hostConfigured && usernameConfigured && passwordConfigured && portConfigured, GetConfiguredSource(hostConfigured, usernameConfigured, passwordConfigured), warnings);
    }

    private static string GetConfiguredSource(bool hostConfigured, bool usernameConfigured, bool passwordConfigured) => hostConfigured && usernameConfigured && passwordConfigured ? "environment_smtp" : "none";
}

public sealed record EmailProviderConfigurationStatus(bool Enabled, bool Configured, string ConfiguredSource, IReadOnlyList<string> Warnings);
