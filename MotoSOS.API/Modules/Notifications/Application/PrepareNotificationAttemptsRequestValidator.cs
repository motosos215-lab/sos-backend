using FluentValidation;
using MotoSOS.API.Modules.Notifications.Contracts;

namespace MotoSOS.API.Modules.Notifications.Application;

public sealed class PrepareNotificationAttemptsRequestValidator : AbstractValidator<PrepareNotificationAttemptsRequest>
{
    public PrepareNotificationAttemptsRequestValidator()
    {
        RuleFor(r => r.AlertDispatchId).NotEmpty();
        RuleFor(r => r.Notes).MaximumLength(500);
    }
}
