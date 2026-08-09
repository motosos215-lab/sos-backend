using FluentValidation;
using MotoSOS.API.Modules.Incidents.Contracts;
using MotoSOS.API.Modules.Incidents.Domain;

namespace MotoSOS.API.Modules.Incidents.Application;

public sealed class CreateIncidentRequestValidator : AbstractValidator<CreateIncidentRequest>
{
    public CreateIncidentRequestValidator()
    {
        RuleFor(r => r.TripId).NotEmpty();
        RuleFor(r => r.ClientIncidentId).NotEmpty().Must(v => Guid.TryParse(v, out _)).WithMessage("ClientIncidentId must be a valid UUID.");
        RuleFor(r => r.Source).NotEmpty().Must(BeEnum<IncidentSource>);
        RuleFor(r => r.Cause).NotEmpty().Must(BeEnum<IncidentCause>);
        RuleFor(r => r.RiskLevel).NotEmpty().Must(BeEnum<IncidentRiskLevel>);
        RuleFor(r => r.OccurredAtUtc).NotNull();
        RuleFor(r => r.Score).InclusiveBetween(0, 100).When(r => r.Score.HasValue);
        RuleFor(r => r.Confidence).InclusiveBetween(0, 1).When(r => r.Confidence.HasValue);
        RuleFor(r => r.GpsQuality).MaximumLength(50);
        RuleFor(r => r.RuleSetVersion).MaximumLength(50);
        RuleFor(r => r.ValidationPolicyVersion).MaximumLength(50);
        RuleFor(r => r.Location).SetValidator(new IncidentLocationRequestValidator()!).When(r => r.Location is not null);
        RuleFor(r => r.EvidenceSummary).SetValidator(new IncidentEvidenceSummaryRequestValidator()!).When(r => r.EvidenceSummary is not null);
    }

    private static bool BeEnum<TEnum>(string? value) where TEnum : struct => Enum.TryParse<TEnum>(value, ignoreCase: true, out _);
}

public sealed class IncidentLocationRequestValidator : AbstractValidator<IncidentLocationRequest>
{
    public IncidentLocationRequestValidator()
    {
        RuleFor(l => l.Latitude).NotNull().InclusiveBetween(-90, 90);
        RuleFor(l => l.Longitude).NotNull().InclusiveBetween(-180, 180);
        RuleFor(l => l.AccuracyMeters).GreaterThanOrEqualTo(0).When(l => l.AccuracyMeters.HasValue);
        RuleFor(l => l.SpeedKmh).GreaterThanOrEqualTo(0).When(l => l.SpeedKmh.HasValue);
        RuleFor(l => l.Provider).MaximumLength(50);
    }
}

public sealed class IncidentEvidenceSummaryRequestValidator : AbstractValidator<IncidentEvidenceSummaryRequest>
{
    public IncidentEvidenceSummaryRequestValidator()
    {
        RuleFor(e => e.AssessmentId).MaximumLength(100);
        RuleFor(e => e.WindowId).MaximumLength(100);
        RuleFor(e => e.TriggeredRules).Must(rules => rules is null || rules.Count <= 20);
        RuleForEach(e => e.TriggeredRules).MaximumLength(80);
        RuleFor(e => e.PhoneBatteryLevel).InclusiveBetween(0, 100).When(e => e.PhoneBatteryLevel.HasValue);
        RuleFor(e => e.WatchBatteryLevel).InclusiveBetween(0, 100).When(e => e.WatchBatteryLevel.HasValue);
        RuleFor(e => e.AppVersion).MaximumLength(50);
    }
}
