using FluentValidation;
using MotoSOS.API.Modules.Trips.Contracts;

namespace MotoSOS.API.Modules.Trips.Application;

public sealed class TripLocationRequestValidator : AbstractValidator<TripLocationRequest>
{
    public TripLocationRequestValidator()
    {
        RuleFor(location => location.Latitude).NotNull().InclusiveBetween(-90, 90);
        RuleFor(location => location.Longitude).NotNull().InclusiveBetween(-180, 180);
        RuleFor(location => location.AccuracyMeters).GreaterThanOrEqualTo(0).When(location => location.AccuracyMeters.HasValue);
        RuleFor(location => location.Provider).MaximumLength(50);
    }
}
