using FluentValidation;
using MotoSOS.API.Modules.Notifications.Contracts;

namespace MotoSOS.API.Modules.Notifications.Application;

public sealed class CancelNotificationAttemptRequestValidator : AbstractValidator<CancelNotificationAttemptRequest>
{
    public CancelNotificationAttemptRequestValidator()
    {
        RuleFor(r => r.Reason).MaximumLength(500);
    }
}
