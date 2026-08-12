using FluentValidation;
using MotoSOS.API.Modules.NotificationPreferences.Contracts;

namespace MotoSOS.API.Modules.NotificationPreferences.Application;

public sealed class UpdateNotificationPreferenceRequestValidator : AbstractValidator<UpdateNotificationPreferenceRequest>
{
    private const string TimePattern = "^([01][0-9]|2[0-3]):[0-5][0-9]$";

    public UpdateNotificationPreferenceRequestValidator()
    {
        RuleFor(request => request.ExtraProperties)
            .Must(extra => extra is null || extra.Count == 0)
            .WithMessage("Unsupported notification preference properties are not allowed.");

        RuleFor(request => request.TimeZone)
            .NotEmpty()
            .MaximumLength(100)
            .Must(BeValidTimeZone)
            .WithMessage("timeZone is invalid.");

        When(request => request.QuietHoursEnabled, () =>
        {
            RuleFor(request => request.QuietHoursStartLocal).NotEmpty().Matches(TimePattern);
            RuleFor(request => request.QuietHoursEndLocal).NotEmpty().Matches(TimePattern);
            RuleFor(request => request).Must(request => request.QuietHoursStartLocal != request.QuietHoursEndLocal).WithMessage("quiet hours start and end must be different.");
        });

        When(request => !request.QuietHoursEnabled, () =>
        {
            RuleFor(request => request.QuietHoursStartLocal).Must(string.IsNullOrWhiteSpace).WithMessage("quietHoursStartLocal must be null when quiet hours are disabled.");
            RuleFor(request => request.QuietHoursEndLocal).Must(string.IsNullOrWhiteSpace).WithMessage("quietHoursEndLocal must be null when quiet hours are disabled.");
        });
    }

    public static bool BeValidTimeZone(string? timeZone)
    {
        if (string.IsNullOrWhiteSpace(timeZone)) return false;
        if (string.Equals(timeZone.Trim(), "America/Mexico_City", StringComparison.OrdinalIgnoreCase)) return true;

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZone.Trim());
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
