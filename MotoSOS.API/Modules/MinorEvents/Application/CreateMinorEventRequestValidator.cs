using FluentValidation;
using MotoSOS.API.Modules.MinorEvents.Contracts;
using MotoSOS.API.Modules.MinorEvents.Domain;

namespace MotoSOS.API.Modules.MinorEvents.Application;

public sealed class CreateMinorEventRequestValidator : AbstractValidator<CreateMinorEventRequest>
{
    public CreateMinorEventRequestValidator()
    {
        RuleFor(x => x.TripId).NotEmpty();
        RuleFor(x => x.ClientEventId).NotEmpty();
        RuleFor(x => x.EventType).NotEmpty().Must(v => Enum.TryParse(v, false, out MinorEventType parsed) && parsed != MinorEventType.Unknown).WithMessage("EventType is invalid.");
        RuleFor(x => x.Severity).NotEmpty().Must(v => Enum.TryParse(v, false, out MinorEventSeverity _)).WithMessage("Severity is invalid.");
        RuleFor(x => x.Source).NotEmpty().Must(v => Enum.TryParse(v, false, out MinorEventSource parsed) && parsed != MinorEventSource.Unknown).WithMessage("Source is invalid.");
        RuleFor(x => x.Confidence).NotNull().InclusiveBetween(0, 1);
        RuleFor(x => x.Score).InclusiveBetween(0, 100).When(x => x.Score.HasValue);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);
        RuleFor(x => x.OccurredAtUtc).NotNull().Must(v => !v.HasValue || v.Value <= DateTimeOffset.UtcNow.AddMinutes(2)).WithMessage("OccurredAtUtc cannot be more than 2 minutes in the future.");
        RuleFor(x => x.Message).MaximumLength(1000);
    }
}
