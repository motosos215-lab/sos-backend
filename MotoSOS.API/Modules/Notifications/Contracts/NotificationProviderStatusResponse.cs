namespace MotoSOS.API.Modules.Notifications.Contracts;

public sealed record NotificationProviderStatusResponse(
    bool SimulatedProviderAvailable,
    bool FcmProviderEnabled,
    bool FcmProviderConfigured,
    bool FcmProjectIdConfigured,
    string FcmCredentialSource,
    bool RealPushEnabled,
    IReadOnlyList<string> Warnings);
