using FluentValidation;
using MotoSOS.API.Modules.Escalations.Contracts;
using MotoSOS.API.Modules.Escalations.Domain;

namespace MotoSOS.API.Modules.Escalations.Application;

public sealed class CreateEmergencyEscalationRequestValidator : AbstractValidator<CreateEmergencyEscalationRequest>
{
    public CreateEmergencyEscalationRequestValidator()
    {
        RuleFor(r => r.Reason).NotEmpty().Must(BeValidReason).WithMessage("reason is invalid.");
        RuleFor(r => r.Level).NotEmpty().Must(BeValidLevel).WithMessage("level is invalid.");
        RuleFor(r => r.Notes).MaximumLength(2000);
    }

    private static bool BeValidReason(string? value) => Enum.TryParse(value, false, out EmergencyEscalationReason reason) && reason != EmergencyEscalationReason.Unknown;
    private static bool BeValidLevel(string? value) => Enum.TryParse(value, false, out EmergencyEscalationLevel _);
}
