namespace MotoSOS.API.Modules.Notifications.Providers;

public interface IFcmPushClient
{
    Task<FcmPushResult> SendAsync(FcmPushRequest request, CancellationToken cancellationToken);
}
