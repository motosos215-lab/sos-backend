namespace MotoSOS.API.Modules.Auth.Application;

public sealed class AuthCodeOptions
{
    public const string SectionName = "AuthCodes";

    public bool Enabled { get; set; } = true;
    public int CodeLength { get; set; } = 6;
    public int TtlMinutes { get; set; } = 10;
    public int MaxAttempts { get; set; } = 5;
    public int RateLimitMinutes { get; set; } = 1;
    public string Provider { get; set; } = "Simulated";
    public AuthCodeEmailOptions Email { get; set; } = new();
}

public sealed class AuthCodeEmailOptions
{
    public bool Enabled { get; set; }
    public string? FromEmail { get; set; }
    public string FromName { get; set; } = "MotoSOS";
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
    public bool UseSsl { get; set; } = true;
}
