using FluentValidation;
using MotoSOS.API.Modules.Notifications.Contracts;

namespace MotoSOS.API.Modules.Notifications.Application;

public sealed class MarkNotificationFailedRequestValidator : AbstractValidator<MarkNotificationFailedRequest>
{
    public MarkNotificationFailedRequestValidator()
    {
        RuleFor(r => r.FailureReason).NotEmpty().MaximumLength(500);
        RuleFor(r => r.Notes).MaximumLength(500);
    }
}
