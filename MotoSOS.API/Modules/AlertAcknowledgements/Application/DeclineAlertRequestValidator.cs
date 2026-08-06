using FluentValidation;
using MotoSOS.API.Modules.AlertAcknowledgements.Contracts;
using MotoSOS.API.Modules.AlertAcknowledgements.Domain;

namespace MotoSOS.API.Modules.AlertAcknowledgements.Application;

public sealed class DeclineAlertRequestValidator : AbstractValidator<DeclineAlertRequest>
{
    public DeclineAlertRequestValidator()
    {
        RuleFor(r => r.ResponseType).NotEmpty().Must(v => Enum.TryParse<AlertAcknowledgementResponseType>(v, true, out _));
        RuleFor(r => r.Message).MaximumLength(500);
    }
}
