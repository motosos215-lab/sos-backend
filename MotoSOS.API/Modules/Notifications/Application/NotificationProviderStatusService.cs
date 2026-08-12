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
    private readonly FcmNotificationProviderOptions _fcmOptions;
    private readonly EmailNotificationProviderOptions _emailOptions;
    private readonly FcmNotificationProviderOptionsValidator _fcmValidator;
    private readonly EmailNotificationProviderOptionsValidator _emailValidator;

    public NotificationProviderStatusService(IUserRepository users, IOptions<FcmNotificationProviderOptions> fcmOptions, IOptions<EmailNotificationProviderOptions> emailOptions, FcmNotificationProviderOptionsValidator fcmValidator, EmailNotificationProviderOptionsValidator emailValidator)
    {
        _users = users;
        _fcmOptions = fcmOptions.Value;
        _emailOptions = emailOptions.Value;
        _fcmValidator = fcmValidator;
        _emailValidator = emailValidator;
    }

    public async Task<NotificationProviderStatusResponse> GetAsync(string adminUserId, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdAsync(adminUserId, cancellationToken);
        if (user is null || !user.IsActive) throw new UnauthorizedAppException("Invalid authentication credentials.");
        if (user.Role != UserRole.Admin) throw new ForbiddenAppException("Notification provider status is available only for admins.");
        FcmProviderConfigurationStatus fcm = _fcmValidator.Validate(_fcmOptions);
        EmailProviderConfigurationStatus email = _emailValidator.Validate(_emailOptions);
        return new NotificationProviderStatusResponse(true, fcm.Enabled, fcm.Configured, fcm.ProjectIdConfigured, fcm.CredentialSource, fcm.Enabled && fcm.Configured, email.Enabled, email.Configured, email.ConfiguredSource, email.Enabled && email.Configured, fcm.Warnings.Concat(email.Warnings).ToArray());
    }
}
