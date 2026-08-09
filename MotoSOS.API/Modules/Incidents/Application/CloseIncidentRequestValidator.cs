using FluentValidation;
using MotoSOS.API.Modules.Incidents.Contracts;

namespace MotoSOS.API.Modules.Incidents.Application;

public sealed class CloseIncidentRequestValidator : AbstractValidator<CloseIncidentRequest>
{
    public CloseIncidentRequestValidator()
    {
        RuleFor(r => r.ClosureReason).MaximumLength(100);
        RuleFor(r => r.ClosureNotes).MaximumLength(500);
    }
}
