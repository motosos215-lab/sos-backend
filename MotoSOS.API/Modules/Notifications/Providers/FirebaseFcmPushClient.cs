using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;

namespace MotoSOS.API.Modules.Notifications.Providers;

public sealed class FirebaseFcmPushClient : IFcmPushClient
{
    private const string AppName = "MotoSOS-Fcm";
    private readonly FcmNotificationProviderOptions _options;
    private readonly FcmNotificationProviderOptionsValidator _validator;
    private readonly object _lock = new();
    private FirebaseMessaging? _messaging;

    public FirebaseFcmPushClient(IOptions<FcmNotificationProviderOptions> options, FcmNotificationProviderOptionsValidator validator)
    {
        _options = options.Value;
        _validator = validator;
    }

    public async Task<FcmPushResult> SendAsync(FcmPushRequest request, CancellationToken cancellationToken)
    {
        FcmProviderConfigurationStatus status = _validator.Validate(_options);
        if (!status.Configured) return new FcmPushResult(false, null, "provider_not_configured", "FCM provider is not configured.");

        try
        {
            FirebaseMessaging messaging = GetMessaging();
#pragma warning disable CS0618
            var message = new Message
            {
                Token = request.RecipientToken,
                Notification = new Notification { Title = request.Title, Body = request.Body },
                Data = request.Data.ToDictionary(item => item.Key, item => item.Value),
                Android = new AndroidConfig { TimeToLive = TimeSpan.FromSeconds(Math.Max(1, request.TtlSeconds)) }
            };
#pragma warning restore CS0618
            string messageId = await messaging.SendAsync(message, dryRun: false, cancellationToken);
            return new FcmPushResult(true, Sanitize(messageId), null, null);
        }
        catch (FirebaseMessagingException exception)
        {
            return new FcmPushResult(false, null, SanitizeCode(exception.MessagingErrorCode.ToString()), "FCM provider failed.");
        }
        catch
        {
            return new FcmPushResult(false, null, "fcm_provider_failed", "FCM provider failed.");
        }
    }

    private FirebaseMessaging GetMessaging()
    {
        if (_messaging is not null) return _messaging;
        lock (_lock)
        {
            if (_messaging is not null) return _messaging;
            FirebaseApp? app = FirebaseApp.GetInstance(AppName);
            if (app is null)
            {
#pragma warning disable CS0618
                GoogleCredential credential = !string.IsNullOrWhiteSpace(_options.ServiceAccountJson)
                    ? GoogleCredential.FromJson(_options.ServiceAccountJson)
                    : GoogleCredential.FromFile(_options.ServiceAccountFilePath);
#pragma warning restore CS0618
                app = FirebaseApp.Create(new AppOptions { Credential = credential, ProjectId = _options.ProjectId }, AppName);
            }
            _messaging = FirebaseMessaging.GetMessaging(app);
            return _messaging;
        }
    }

    private static string? Sanitize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, 200)];
    private static string SanitizeCode(string? value) => string.IsNullOrWhiteSpace(value) ? "fcm_provider_failed" : value.Trim().ToLowerInvariant().Replace(' ', '_');
}
