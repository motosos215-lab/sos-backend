namespace MotoSOS.API.Modules.Auth.Application;

public sealed class AuthCodeEmailOptionsValidator
{
    public AuthCodeEmailConfigurationStatus Validate(AuthCodeEmailOptions options)
    {
        var warnings = new List<string>();
        if (!options.Enabled) warnings.Add("email_disabled");
        if (string.IsNullOrWhiteSpace(options.FromEmail)) warnings.Add("from_email_missing");
        if (string.IsNullOrWhiteSpace(options.SmtpHost)) warnings.Add("smtp_host_missing");
        if (options.SmtpPort <= 0) warnings.Add("smtp_port_invalid");
        if (string.IsNullOrWhiteSpace(options.SmtpUsername)) warnings.Add("smtp_username_missing");
        if (string.IsNullOrWhiteSpace(options.SmtpPassword)) warnings.Add("smtp_password_missing");

        return new AuthCodeEmailConfigurationStatus(options.Enabled, warnings.Count == 0, warnings);
    }
}

public sealed record AuthCodeEmailConfigurationStatus(bool Enabled, bool Configured, IReadOnlyList<string> Warnings);
