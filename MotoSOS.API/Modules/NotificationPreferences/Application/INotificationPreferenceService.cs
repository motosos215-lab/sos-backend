using MotoSOS.API.Modules.NotificationPreferences.Contracts;

namespace MotoSOS.API.Modules.NotificationPreferences.Application;

public interface INotificationPreferenceService
{
    Task<GetNotificationPreferenceResponse> GetMineAsync(string userId, CancellationToken cancellationToken);

    Task<UpdateNotificationPreferenceResponse> UpdateMineAsync(string userId, UpdateNotificationPreferenceRequest request, CancellationToken cancellationToken);
}
