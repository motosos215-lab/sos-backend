using FluentValidation;
using MotoSOS.API.Modules.AlertDispatch.Contracts;

namespace MotoSOS.API.Modules.AlertDispatch.Application;

public sealed class CancelAlertDispatchRequestValidator : AbstractValidator<CancelAlertDispatchRequest>
{
    public CancelAlertDispatchRequestValidator()
    {
        RuleFor(r => r.Reason).MaximumLength(500);
    }
}
