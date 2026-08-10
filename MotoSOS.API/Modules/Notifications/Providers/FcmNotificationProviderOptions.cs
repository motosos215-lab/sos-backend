namespace MotoSOS.API.Modules.Notifications.Providers;

public sealed class FcmNotificationProviderOptions
{
    public const string SectionName = "Notifications:Providers:Fcm";
    public bool Enabled { get; set; }
    public string? ProjectId { get; set; }
    public string? ServiceAccountJson { get; set; }
    public string? ServiceAccountJsonBase64 { get; set; }
    public string? ServiceAccountFilePath { get; set; }
    public string DefaultTitle { get; set; } = "MotoSOS Alert";
    public int DefaultTtlSeconds { get; set; } = 3600;
}
