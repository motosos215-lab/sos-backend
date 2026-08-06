using FluentValidation;
using MotoSOS.API.Modules.AlertDispatch.Contracts;
using MotoSOS.API.Modules.AlertDispatch.Domain;

namespace MotoSOS.API.Modules.AlertDispatch.Application;

public sealed class CreateAlertDispatchRequestValidator : AbstractValidator<CreateAlertDispatchRequest>
{
    public CreateAlertDispatchRequestValidator()
    {
        RuleFor(r => r.IncidentId).NotEmpty();
        RuleFor(r => r.ClientAlertRequestId).NotEmpty().Must(v => Guid.TryParse(v, out _)).WithMessage("ClientAlertRequestId must be a valid UUID.");
        RuleFor(r => r.Priority).NotEmpty().Must(BeEnum<AlertDispatchPriority>);
        RuleFor(r => r.Reason).NotEmpty().Must(BeEnum<AlertDispatchReason>);
        RuleFor(r => r.RequestedAtUtc).NotNull();
        RuleFor(r => r.Notes).MaximumLength(500);
    }

    private static bool BeEnum<TEnum>(string? value) where TEnum : struct => Enum.TryParse<TEnum>(value, ignoreCase: true, out _);
}
