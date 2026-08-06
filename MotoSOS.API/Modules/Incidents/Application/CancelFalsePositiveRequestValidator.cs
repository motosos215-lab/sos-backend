using FluentValidation;
using MotoSOS.API.Modules.Incidents.Contracts;

namespace MotoSOS.API.Modules.Incidents.Application;

public sealed class CancelFalsePositiveRequestValidator : AbstractValidator<CancelFalsePositiveRequest>
{
    public CancelFalsePositiveRequestValidator()
    {
        RuleFor(r => r.Reason).MaximumLength(500);
    }
}
