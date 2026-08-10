using MotoSOS.API.Modules.Notifications.Contracts;

namespace MotoSOS.API.Modules.Notifications.Application;

public interface INotificationProviderStatusService
{
    Task<NotificationProviderStatusResponse> GetAsync(string adminUserId, CancellationToken cancellationToken);
}
