using FluentValidation;
using MotoSOS.API.Modules.Profiles.Contracts;

namespace MotoSOS.API.Modules.Profiles.Application;

public sealed class UpsertMyProfileRequestValidator : AbstractValidator<UpsertMyProfileRequest>
{
    private const string PhonePattern = "^[+0-9 ()-]{7,20}$";

    public UpsertMyProfileRequestValidator()
    {
        RuleFor(request => request.SaveMode)
            .NotEmpty()
            .Must(IsAllowedSaveMode)
            .WithMessage("Save mode must be Draft or Continue.");

        RuleFor(request => request.FullName)
            .MaximumLength(150)
            .When(request => !string.IsNullOrWhiteSpace(request.FullName));

        RuleFor(request => request.PhoneNumber)
            .Matches(PhonePattern)
            .When(request => !string.IsNullOrWhiteSpace(request.PhoneNumber));

        RuleFor(request => request.ProvisionalEmergencyContactPhone)
            .Matches(PhonePattern)
            .When(request => !string.IsNullOrWhiteSpace(request.ProvisionalEmergencyContactPhone));

        RuleFor(request => request.CurpOrIdentifier)
            .MaximumLength(50)
            .When(request => !string.IsNullOrWhiteSpace(request.CurpOrIdentifier));

        RuleFor(request => request.AddressOrZone)
            .MaximumLength(250)
            .When(request => !string.IsNullOrWhiteSpace(request.AddressOrZone));

        RuleFor(request => request.PrimaryCity)
            .MaximumLength(120)
            .When(request => !string.IsNullOrWhiteSpace(request.PrimaryCity));

        RuleFor(request => request.BloodType)
            .MaximumLength(10)
            .When(request => !string.IsNullOrWhiteSpace(request.BloodType));

        RuleFor(request => request.Allergies)
            .MaximumLength(500)
            .When(request => !string.IsNullOrWhiteSpace(request.Allergies));

        RuleFor(request => request.MedicalConditions)
            .MaximumLength(500)
            .When(request => !string.IsNullOrWhiteSpace(request.MedicalConditions));

        RuleFor(request => request.ProvisionalEmergencyContactName)
            .MaximumLength(150)
            .When(request => !string.IsNullOrWhiteSpace(request.ProvisionalEmergencyContactName));

        When(IsContinue, () =>
        {
            RuleFor(request => request.FullName).NotEmpty();
            RuleFor(request => request.PhoneNumber).NotEmpty().Matches(PhonePattern);
            RuleFor(request => request.DateOfBirth).NotNull();
            RuleFor(request => request.AddressOrZone).NotEmpty();
            RuleFor(request => request.PrimaryCity).NotEmpty();
            RuleFor(request => request.ProvisionalEmergencyContactName).NotEmpty();
            RuleFor(request => request.ProvisionalEmergencyContactPhone).NotEmpty().Matches(PhonePattern);
        });
    }

    private static bool IsContinue(UpsertMyProfileRequest request) =>
        string.Equals(request.SaveMode?.Trim(), nameof(SaveMode.Continue), StringComparison.OrdinalIgnoreCase);

    private static bool IsAllowedSaveMode(string? saveMode) =>
        string.Equals(saveMode?.Trim(), nameof(SaveMode.Draft), StringComparison.OrdinalIgnoreCase) ||
        string.Equals(saveMode?.Trim(), nameof(SaveMode.Continue), StringComparison.OrdinalIgnoreCase);
}
