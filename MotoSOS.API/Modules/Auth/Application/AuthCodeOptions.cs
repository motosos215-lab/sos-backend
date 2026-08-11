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
}
