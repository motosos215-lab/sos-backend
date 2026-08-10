using MotoSOS.API.Modules.PushNotificationTokens.Domain;

namespace MotoSOS.API.Modules.PushNotificationTokens.Application;

public interface IPushNotificationTokenIdempotencyKeyFactory
{
    string Create(string userId, string tokenHash, PushTokenPlatform platform, PushTokenChannel channel, string? deviceId);
}
