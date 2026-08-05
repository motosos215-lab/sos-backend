using FluentValidation;
using MotoSOS.API.Modules.EmergencyContacts.Contracts;

namespace MotoSOS.API.Modules.EmergencyContacts.Application;

public sealed class UpdateEmergencyContactRequestValidator : AbstractValidator<UpdateEmergencyContactRequest>
{
    public UpdateEmergencyContactRequestValidator()
    {
        EmergencyContactValidationRules.Apply(this);
    }
}
