using FluentValidation;
using MotoSOS.API.Modules.EmergencyResolution.Contracts;
using MotoSOS.API.Modules.EmergencyResolution.Domain;

namespace MotoSOS.API.Modules.EmergencyResolution.Application;

public sealed class CreateEmergencyResolutionReportRequestValidator : AbstractValidator<CreateEmergencyResolutionReportRequest>
{
    public CreateEmergencyResolutionReportRequestValidator()
    {
        RuleFor(r => r.Outcome).NotEmpty().Must(value => Enum.TryParse<EmergencyResolutionOutcome>(value, true, out EmergencyResolutionOutcome outcome) && outcome != EmergencyResolutionOutcome.Unknown).WithMessage("Outcome is invalid.");
        RuleFor(r => r.Summary).NotEmpty().MaximumLength(1000);
        RuleFor(r => r.Notes).MaximumLength(2000);
    }
}
