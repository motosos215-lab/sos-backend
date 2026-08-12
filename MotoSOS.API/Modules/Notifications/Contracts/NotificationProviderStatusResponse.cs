namespace MotoSOS.API.Modules.Notifications.Contracts;

public sealed record NotificationProviderStatusResponse(
    bool SimulatedProviderAvailable,
    bool FcmProviderEnabled,
    bool FcmProviderConfigured,
    bool FcmProjectIdConfigured,
    string FcmCredentialSource,
    bool RealPushEnabled,
    bool EmailProviderEnabled,
    bool EmailProviderConfigured,
    string EmailConfiguredSource,
    bool RealEmailEnabled,
    bool SmsProviderEnabled,
    bool SmsProviderConfigured,
    string SmsProviderName,
    bool RealSmsEnabled,
    IReadOnlyList<string> Warnings);
