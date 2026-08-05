using FluentValidation;
using MotoSOS.API.Modules.Vehicles.Contracts;

namespace MotoSOS.API.Modules.Vehicles.Application;

public sealed class UpdateVehicleRequestValidator : AbstractValidator<UpdateVehicleRequest>
{
    public UpdateVehicleRequestValidator()
    {
        VehicleValidationRules.Apply(this);
    }
}
