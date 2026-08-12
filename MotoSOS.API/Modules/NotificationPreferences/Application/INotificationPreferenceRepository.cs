using MotoSOS.API.Modules.NotificationPreferences.Domain;

namespace MotoSOS.API.Modules.NotificationPreferences.Application;

public interface INotificationPreferenceRepository
{
    Task<NotificationPreference?> GetByUserIdAsync(string userId, CancellationToken cancellationToken);

    Task AddAsync(NotificationPreference preference, CancellationToken cancellationToken);

    Task UpdateAsync(NotificationPreference preference, CancellationToken cancellationToken);
}
