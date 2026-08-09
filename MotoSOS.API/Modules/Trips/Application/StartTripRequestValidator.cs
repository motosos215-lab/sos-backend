using FluentValidation;
using MotoSOS.API.Modules.Trips.Contracts;

namespace MotoSOS.API.Modules.Trips.Application;

public sealed class StartTripRequestValidator : AbstractValidator<StartTripRequest>
{
    public StartTripRequestValidator()
    {
        RuleFor(request => request.VehicleId).NotEmpty();
        RuleFor(request => request.MobileDeviceId).NotEmpty();
        RuleFor(request => request.BatteryLevel).InclusiveBetween(0, 100).When(request => request.BatteryLevel.HasValue);
        RuleFor(request => request.AppVersion).MaximumLength(50);
        RuleFor(request => request.StartLocation).SetValidator(new TripLocationRequestValidator()!).When(request => request.StartLocation is not null);
    }
}
