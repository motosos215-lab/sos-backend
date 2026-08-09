namespace MotoSOS.API.Modules.PushNotificationTokens.Application;

public interface IPushNotificationTokenHasher
{
    string Hash(string token);
}
