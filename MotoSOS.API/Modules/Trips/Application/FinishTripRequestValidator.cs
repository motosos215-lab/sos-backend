using FluentValidation;
using MotoSOS.API.Modules.Trips.Contracts;

namespace MotoSOS.API.Modules.Trips.Application;

public sealed class FinishTripRequestValidator : AbstractValidator<FinishTripRequest>
{
    public FinishTripRequestValidator()
    {
        RuleFor(request => request.BatteryLevel).InclusiveBetween(0, 100).When(request => request.BatteryLevel.HasValue);
        RuleFor(request => request.Notes).MaximumLength(500);
        RuleFor(request => request.EndLocation).SetValidator(new TripLocationRequestValidator()!).When(request => request.EndLocation is not null);
    }
}
