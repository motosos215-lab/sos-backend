using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.PushNotificationTokens.Domain;

namespace MotoSOS.API.Modules.PushNotificationTokens.Application;

public sealed class PushNotificationTokenQueryValidator
{
    public PushNotificationTokenQuery Validate(string? userId, string? platform, string? channel, string? status, DateTimeOffset? dateFrom, DateTimeOffset? dateTo, int? pageNumber, int? pageSize)
    {
        int page = pageNumber ?? 1;
        int size = pageSize ?? 20;
        if (page < 1) throw new ValidationAppException("pageNumber must be greater than or equal to 1.");
        if (size < 1 || size > 100) throw new ValidationAppException("pageSize must be between 1 and 100.");
        if (dateFrom.HasValue && dateTo.HasValue && dateFrom > dateTo) throw new ValidationAppException("dateFrom must be less than or equal to dateTo.");

        return new PushNotificationTokenQuery(
            NormalizeOptional(userId),
            ParseOptional<PushTokenPlatform>(platform, nameof(platform)),
            ParseOptional<PushTokenChannel>(channel, nameof(channel)),
            ParseOptional<PushNotificationTokenStatus>(status, nameof(status)),
            dateFrom,
            dateTo,
            page,
            size);
    }

    private static TEnum? ParseOptional<TEnum>(string? value, string name) where TEnum : struct
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!Enum.TryParse(value.Trim(), ignoreCase: true, out TEnum parsed)) throw new ValidationAppException($"{name} must be a valid value.");
        return parsed;
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
