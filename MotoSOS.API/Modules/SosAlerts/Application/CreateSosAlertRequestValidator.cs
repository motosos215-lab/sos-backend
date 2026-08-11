using FluentValidation;
using MotoSOS.API.Modules.AlertDispatch.Domain;
using MotoSOS.API.Modules.Incidents.Domain;
using MotoSOS.API.Modules.SosAlerts.Contracts;

namespace MotoSOS.API.Modules.SosAlerts.Application;

public sealed class CreateSosAlertRequestValidator : AbstractValidator<CreateSosAlertRequest>
{
    public CreateSosAlertRequestValidator()
    {
        RuleFor(r => r.TripId).NotEmpty();
        RuleFor(r => r.ClientIncidentId).NotEmpty().Must(v => Guid.TryParse(v, out _)).WithMessage("ClientIncidentId must be a valid UUID.");
        RuleFor(r => r.ClientAlertRequestId).NotEmpty().Must(v => Guid.TryParse(v, out _)).WithMessage("ClientAlertRequestId must be a valid UUID.");
        RuleFor(r => r.IncidentType).NotEmpty().Must(BeEnum<IncidentCause>);
        RuleFor(r => r.Severity).NotEmpty().Must(BeEnum<IncidentRiskLevel>);
        RuleFor(r => r.DetectedAtUtc).NotNull();
        RuleFor(r => r.Latitude).NotNull().InclusiveBetween(-90, 90);
        RuleFor(r => r.Longitude).NotNull().InclusiveBetween(-180, 180);
        RuleFor(r => r.Priority).NotEmpty().Must(BeEnum<AlertDispatchPriority>);
        RuleFor(r => r.Reason).NotEmpty().Must(BeEnum<AlertDispatchReason>);
        RuleFor(r => r.Notes).MaximumLength(500);
    }

    private static bool BeEnum<TEnum>(string? value) where TEnum : struct => Enum.TryParse<TEnum>(value, ignoreCase: true, out _);
}
