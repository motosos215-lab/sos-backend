using FluentValidation;
using MotoSOS.API.Modules.Notifications.Contracts;

namespace MotoSOS.API.Modules.Notifications.Application;

public sealed class MarkNotificationSimulatedSentRequestValidator : AbstractValidator<MarkNotificationSimulatedSentRequest>
{
    public MarkNotificationSimulatedSentRequestValidator()
    {
        RuleFor(r => r.ProviderMessageId).MaximumLength(200);
        RuleFor(r => r.Notes).MaximumLength(500);
    }
}
