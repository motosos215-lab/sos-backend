namespace MotoSOS.API.Modules.PushNotificationTokens.Application;

public interface IPushNotificationTokenPreviewer
{
    string CreatePreview(string token);
}
