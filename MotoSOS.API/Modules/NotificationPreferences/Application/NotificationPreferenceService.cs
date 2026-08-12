using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.NotificationPreferences.Contracts;
using MotoSOS.API.Modules.NotificationPreferences.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Modules.NotificationPreferences.Application;

public sealed class NotificationPreferenceService : INotificationPreferenceService
{
    private const string DefaultTimeZone = "America/Mexico_City";

    private readonly IUserRepository _users;
    private readonly INotificationPreferenceRepository _preferences;
    private readonly IClock _clock;

    public NotificationPreferenceService(IUserRepository users, INotificationPreferenceRepository preferences, IClock clock)
    {
        _users = users;
        _preferences = preferences;
        _clock = clock;
    }

    public async Task<GetNotificationPreferenceResponse> GetMineAsync(string userId, CancellationToken cancellationToken)
    {
        User user = await GetActiveUserAsync(userId, cancellationToken);
        NotificationPreference preference = await GetOrCreateDefaultAsync(user, cancellationToken);
        return new GetNotificationPreferenceResponse(ToResponse(preference));
    }

    public async Task<UpdateNotificationPreferenceResponse> UpdateMineAsync(string userId, UpdateNotificationPreferenceRequest request, CancellationToken cancellationToken)
    {
        User user = await GetActiveUserAsync(userId, cancellationToken);
        NotificationPreference preference = await GetOrCreateDefaultAsync(user, cancellationToken);
        DateTimeOffset now = _clock.UtcNow;

        preference.PushEnabled = request.PushEnabled;
        preference.EmailEnabled = request.EmailEnabled;
        preference.SmsEnabled = request.SmsEnabled;
        preference.CriticalAlertsEnabled = request.CriticalAlertsEnabled;
        preference.TripUpdatesEnabled = request.TripUpdatesEnabled;
        preference.SecurityAlertsEnabled = request.SecurityAlertsEnabled;
        preference.MarketingEnabled = request.MarketingEnabled;
        preference.QuietHoursEnabled = request.QuietHoursEnabled;
        preference.QuietHoursStartLocal = NormalizeOptional(request.QuietHoursStartLocal);
        preference.QuietHoursEndLocal = NormalizeOptional(request.QuietHoursEndLocal);
        preference.TimeZone = request.TimeZone.Trim();
        preference.UpdatedAtUtc = now;

        await _preferences.UpdateAsync(preference, cancellationToken);
        return new UpdateNotificationPreferenceResponse(ToResponse(preference));
    }

    public static NotificationPreference CreateDefault(string userId, DateTimeOffset now) => new()
    {
        UserId = userId,
        PushEnabled = true,
        EmailEnabled = false,
        SmsEnabled = false,
        CriticalAlertsEnabled = true,
        TripUpdatesEnabled = true,
        SecurityAlertsEnabled = true,
        MarketingEnabled = false,
        QuietHoursEnabled = false,
        QuietHoursStartLocal = null,
        QuietHoursEndLocal = null,
        TimeZone = DefaultTimeZone,
        CreatedAtUtc = now,
        UpdatedAtUtc = now
    };

    private async Task<NotificationPreference> GetOrCreateDefaultAsync(User user, CancellationToken cancellationToken)
    {
        NotificationPreference? existing = await _preferences.GetByUserIdAsync(user.Id, cancellationToken);
        if (existing is not null) return existing;

        NotificationPreference preference = CreateDefault(user.Id, _clock.UtcNow);
        await _preferences.AddAsync(preference, cancellationToken);
        return preference;
    }

    private async Task<User> GetActiveUserAsync(string userId, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive) throw new UnauthorizedAppException("Invalid authentication credentials.");
        return user;
    }

    private static NotificationPreferenceResponse ToResponse(NotificationPreference preference) => new(
        preference.PushEnabled,
        preference.EmailEnabled,
        preference.SmsEnabled,
        preference.CriticalAlertsEnabled,
        preference.TripUpdatesEnabled,
        preference.SecurityAlertsEnabled,
        preference.MarketingEnabled,
        preference.QuietHoursEnabled,
        preference.QuietHoursStartLocal,
        preference.QuietHoursEndLocal,
        preference.TimeZone,
        preference.CreatedAtUtc,
        preference.UpdatedAtUtc);

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
