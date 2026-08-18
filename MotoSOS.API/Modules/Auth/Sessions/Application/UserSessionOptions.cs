namespace MotoSOS.API.Modules.Auth.Sessions.Application;

public sealed class UserSessionOptions
{
    public const string SectionName = "Auth:Sessions";
    public int TakeoverTokenTtlMinutes { get; set; } = 3;
    public int LastSeenUpdateThrottleMinutes { get; set; } = 5;
}
