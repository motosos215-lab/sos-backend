namespace MotoSOS.API.Modules.Notifications.Providers;

public sealed class SmsNotificationProviderOptions
{
    public const string SectionName = "Notifications:Providers:Sms";

    public bool Enabled { get; set; }
    public string Provider { get; set; } = "Brevo";
    public string? ApiKey { get; set; }
    public string Sender { get; set; } = "MotoSOS";
    public string? DefaultCountryCode { get; set; } = "+52";
    public int TimeoutSeconds { get; set; } = 15;
}
