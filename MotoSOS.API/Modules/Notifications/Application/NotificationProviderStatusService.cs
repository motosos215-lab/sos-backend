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
    private readonly SmsNotificationProviderOptions _smsOptions;
    private readonly FcmNotificationProviderOptionsValidator _fcmValidator;
    private readonly EmailNotificationProviderOptionsValidator _emailValidator;
    private readonly SmsNotificationProviderOptionsValidator _smsValidator;

    public NotificationProviderStatusService(IUserRepository users, IOptions<FcmNotificationProviderOptions> fcmOptions, IOptions<EmailNotificationProviderOptions> emailOptions, IOptions<SmsNotificationProviderOptions> smsOptions, FcmNotificationProviderOptionsValidator fcmValidator, EmailNotificationProviderOptionsValidator emailValidator, SmsNotificationProviderOptionsValidator smsValidator)
    {
        _users = users;
        _fcmOptions = fcmOptions.Value;
        _emailOptions = emailOptions.Value;
        _smsOptions = smsOptions.Value;
        _fcmValidator = fcmValidator;
        _emailValidator = emailValidator;
        _smsValidator = smsValidator;
    }

    public async Task<NotificationProviderStatusResponse> GetAsync(string adminUserId, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdAsync(adminUserId, cancellationToken);
        if (user is null || !user.IsActive) throw new UnauthorizedAppException("Invalid authentication credentials.");
        if (user.Role != UserRole.Admin) throw new ForbiddenAppException("Notification provider status is available only for admins.");
        FcmProviderConfigurationStatus fcm = _fcmValidator.Validate(_fcmOptions);
        EmailProviderConfigurationStatus email = _emailValidator.Validate(_emailOptions);
        SmsProviderConfigurationStatus sms = _smsValidator.Validate(_smsOptions);
        return new NotificationProviderStatusResponse(true, fcm.Enabled, fcm.Configured, fcm.ProjectIdConfigured, fcm.CredentialSource, fcm.Enabled && fcm.Configured, email.Enabled, email.Configured, email.ConfiguredSource, email.Enabled && email.Configured, sms.Enabled, sms.Configured, sms.ProviderName, sms.Enabled && sms.Configured, fcm.Warnings.Concat(email.Warnings).Concat(sms.Warnings).ToArray());
    }
}
