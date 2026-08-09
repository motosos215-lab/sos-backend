using FluentValidation;
using MotoSOS.API.Modules.NotificationOutbox.Contracts;

namespace MotoSOS.API.Modules.NotificationOutbox.Application;

public sealed class RunNotificationOutboxRequestValidator : AbstractValidator<RunNotificationOutboxRequest>
{
    public RunNotificationOutboxRequestValidator()
    {
        RuleFor(r => r.MaxItems).InclusiveBetween(1, 100).When(r => r.MaxItems.HasValue);
    }
}
