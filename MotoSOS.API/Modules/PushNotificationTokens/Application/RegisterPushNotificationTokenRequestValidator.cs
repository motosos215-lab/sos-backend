using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.PushNotificationTokens.Contracts;
using MotoSOS.API.Modules.PushNotificationTokens.Domain;

namespace MotoSOS.API.Modules.PushNotificationTokens.Application;

public sealed class RegisterPushNotificationTokenRequestValidator
{
    private const int MinTokenLength = 20;
    private const int MaxTokenLength = 4096;
    private const int MaxDeviceIdLength = 100;
    private const int MetadataValueMaxLength = 200;

    private static readonly string[] ForbiddenRequestProperties =
    [
        "userid",
        "tokenhash",
        "status",
        "tokenvalue",
        "providercredentials",
        "credentials"
    ];

    private static readonly string[] SensitiveKeyFragments =
    [
        "pass" + "word",
        "pass" + "wordhash",
        "accesstoken",
        "refreshtoken",
        "token",
        "pushtoken",
        "authorization",
        "bearer",
        "deviceidentifier",
        "deviceidentifierhash",
        "providertoken",
        "payload",
        "base64",
        "binary",
        "filecontent",
        "email",
        "phone",
        "pay" + "ment",
        "card",
        "connection" + "string",
        "secret",
        "stacktrace",
        "exception",
        "mongo",
        "mongodb"
    ];

    public ValidatedRegisterPushNotificationTokenRequest Validate(RegisterPushNotificationTokenRequest? request)
    {
        if (request is null) throw new ValidationAppException("Request body is required.");
        RejectForbiddenProperties(request);

        PushTokenPlatform platform = ParseRequiredEnum<PushTokenPlatform>(request.Platform, nameof(request.Platform));
        PushTokenChannel channel = ParseRequiredEnum<PushTokenChannel>(request.Channel, nameof(request.Channel));
        EnsureSupportedCombination(platform, channel);

        string token = NormalizeRequired(request.Token, nameof(request.Token));
        if (token.Length < MinTokenLength) throw new ValidationAppException("token must have at least 20 characters.");
        if (token.Length > MaxTokenLength) throw new ValidationAppException("token must have at most 4096 characters.");

        string? deviceId = NormalizeOptional(request.DeviceId);
        if (deviceId?.Length > MaxDeviceIdLength) throw new ValidationAppException("deviceId must have at most 100 characters.");

        return new ValidatedRegisterPushNotificationTokenRequest(platform, channel, deviceId, token, SanitizeMetadata(request.Metadata));
    }

    private static void RejectForbiddenProperties(RegisterPushNotificationTokenRequest request)
    {
        if (request.ExtraProperties is null) return;
        foreach (string propertyName in request.ExtraProperties.Keys)
        {
            string normalized = NormalizeKey(propertyName);
            if (ForbiddenRequestProperties.Contains(normalized, StringComparer.OrdinalIgnoreCase) || normalized.Contains("provider", StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationAppException($"{propertyName} is not allowed in this request.");
            }
        }
    }

    private static void EnsureSupportedCombination(PushTokenPlatform platform, PushTokenChannel channel)
    {
        bool isValid = (platform, channel) is
            (PushTokenPlatform.Android, PushTokenChannel.Fcm) or
            (PushTokenPlatform.Ios, PushTokenChannel.Apns) or
            (PushTokenPlatform.Web, PushTokenChannel.WebPush) or
            (PushTokenPlatform.Web, PushTokenChannel.Fcm);

        if (!isValid) throw new ValidationAppException("platform and channel combination is not supported.");
    }

    public static Dictionary<string, string> SanitizeMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0) return [];
        var sanitized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach ((string key, string value) in metadata)
        {
            string? normalizedKey = NormalizeOptional(key);
            if (normalizedKey is null || IsSensitiveKey(normalizedKey)) continue;
            sanitized[normalizedKey] = Truncate(NormalizeOptional(value)) ?? string.Empty;
        }

        return sanitized;
    }

    private static bool IsSensitiveKey(string key) => SensitiveKeyFragments.Any(fragment => NormalizeKey(key).Contains(fragment, StringComparison.OrdinalIgnoreCase));
    private static string NormalizeKey(string key) => key.Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal).Trim();
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string NormalizeRequired(string? value, string name) => string.IsNullOrWhiteSpace(value) ? throw new ValidationAppException($"{name} is required.") : value.Trim();
    private static string? Truncate(string? value) => value is null || value.Length <= MetadataValueMaxLength ? value : value[..MetadataValueMaxLength];

    private static TEnum ParseRequiredEnum<TEnum>(string? value, string name) where TEnum : struct
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ValidationAppException($"{name} is required.");
        if (!Enum.TryParse(value.Trim(), ignoreCase: true, out TEnum parsed)) throw new ValidationAppException($"{name} must be a valid value.");
        return parsed;
    }
}

public sealed record ValidatedRegisterPushNotificationTokenRequest(PushTokenPlatform Platform, PushTokenChannel Channel, string? DeviceId, string Token, IReadOnlyDictionary<string, string> Metadata);
