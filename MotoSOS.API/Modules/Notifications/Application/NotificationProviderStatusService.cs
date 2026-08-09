using Microsoft.Extensions.Options;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.Notifications.Contracts;
using MotoSOS.API.Modules.Notifications.Providers;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Modules.Notifications.Application;

public sealed class NotificationProviderStatusService : INotificationProviderStatusService
{
    private readonly IUserRepository _users;
    private readonly FcmNotificationProviderOptions _options;
    private readonly FcmNotificationProviderOptionsValidator _validator;

    public NotificationProviderStatusService(IUserRepository users, IOptions<FcmNotificationProviderOptions> options, FcmNotificationProviderOptionsValidator validator)
    {
        _users = users;
        _options = options.Value;
        _validator = validator;
    }

    public async Task<NotificationProviderStatusResponse> GetAsync(string adminUserId, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdAsync(adminUserId, cancellationToken);
        if (user is null || !user.IsActive) throw new UnauthorizedAppException("Invalid authentication credentials.");
        if (user.Role != UserRole.Admin) throw new ForbiddenAppException("Notification provider status is available only for admins.");
        FcmProviderConfigurationStatus status = _validator.Validate(_options);
        return new NotificationProviderStatusResponse(true, status.Enabled, status.Configured, status.ProjectIdConfigured, status.CredentialSource, status.Enabled && status.Configured, status.Warnings);
    }
}
