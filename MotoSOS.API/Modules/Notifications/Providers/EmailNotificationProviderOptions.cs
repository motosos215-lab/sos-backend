namespace MotoSOS.API.Modules.Notifications.Providers;

public sealed class EmailNotificationProviderOptions
{
    public const string SectionName = "Notifications:Providers:Email";

    public bool Enabled { get; set; }
    public string? FromEmail { get; set; }
    public string FromName { get; set; } = "MotoSOS";
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
    public bool UseSsl { get; set; } = true;
}
