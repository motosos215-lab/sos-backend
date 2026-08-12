using System.Text.RegularExpressions;

namespace MotoSOS.API.Modules.Notifications.Providers;

public sealed partial class SmsNotificationProviderOptionsValidator
{
    public SmsProviderConfigurationStatus Validate(SmsNotificationProviderOptions options)
    {
        if (!options.Enabled) return new SmsProviderConfigurationStatus(false, false, NormalizeProviderName(options.Provider), ["sms_disabled"]);

        var warnings = new List<string>();
        string providerName = NormalizeProviderName(options.Provider);
        if (!string.Equals(providerName, "Brevo", StringComparison.OrdinalIgnoreCase)) warnings.Add("sms_provider_not_supported");
        if (string.IsNullOrWhiteSpace(options.ApiKey)) warnings.Add("sms_api_key_missing");
        if (string.IsNullOrWhiteSpace(options.Sender)) warnings.Add("sms_sender_missing");
        if (options.TimeoutSeconds <= 0) warnings.Add("sms_timeout_invalid");
        if (!string.IsNullOrWhiteSpace(options.DefaultCountryCode) && !CountryCodeRegex().IsMatch(options.DefaultCountryCode.Trim())) warnings.Add("sms_default_country_code_invalid");

        return new SmsProviderConfigurationStatus(options.Enabled, warnings.Count == 0, providerName, warnings);
    }

    private static string NormalizeProviderName(string? provider) => string.IsNullOrWhiteSpace(provider) ? "none" : provider.Trim();

    [GeneratedRegex("^\\+[1-9][0-9]{0,3}$", RegexOptions.CultureInvariant)]
    private static partial Regex CountryCodeRegex();
}

public sealed record SmsProviderConfigurationStatus(bool Enabled, bool Configured, string ProviderName, IReadOnlyList<string> Warnings);
