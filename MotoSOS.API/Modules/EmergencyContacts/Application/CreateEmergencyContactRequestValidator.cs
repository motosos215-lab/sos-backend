using FluentValidation;
using MotoSOS.API.Modules.EmergencyContacts.Contracts;

namespace MotoSOS.API.Modules.EmergencyContacts.Application;

public sealed class CreateEmergencyContactRequestValidator : AbstractValidator<CreateEmergencyContactRequest>
{
    public CreateEmergencyContactRequestValidator()
    {
        EmergencyContactValidationRules.Apply(this);
    }
}

internal static class EmergencyContactValidationRules
{
    private const string PhonePattern = "^[+0-9 ()-]{7,20}$";

    public static void Apply<TRequest>(AbstractValidator<TRequest> validator)
        where TRequest : class
    {
        validator.RuleFor(request => GetSaveMode(request))
            .NotEmpty()
            .Must(IsAllowedSaveMode)
            .WithName(nameof(CreateEmergencyContactRequest.SaveMode))
            .WithMessage("Save mode must be Draft or Continue.");

        validator.RuleFor(request => GetFullName(request)).MaximumLength(150).When(request => !string.IsNullOrWhiteSpace(GetFullName(request))).WithName(nameof(CreateEmergencyContactRequest.FullName));
        validator.RuleFor(request => GetRelationship(request)).MaximumLength(80).When(request => !string.IsNullOrWhiteSpace(GetRelationship(request))).WithName(nameof(CreateEmergencyContactRequest.Relationship));
        validator.RuleFor(request => GetPhoneNumber(request)).Matches(PhonePattern).When(request => !string.IsNullOrWhiteSpace(GetPhoneNumber(request))).WithName(nameof(CreateEmergencyContactRequest.PhoneNumber));
        validator.RuleFor(request => GetEmail(request)).EmailAddress().When(request => !string.IsNullOrWhiteSpace(GetEmail(request))).WithName(nameof(CreateEmergencyContactRequest.Email));
        validator.RuleFor(request => GetPriority(request)).GreaterThanOrEqualTo(1).When(request => GetPriority(request).HasValue).WithName(nameof(CreateEmergencyContactRequest.Priority));

        validator.When(IsContinue, () =>
        {
            validator.RuleFor(request => GetFullName(request)).NotEmpty().WithName(nameof(CreateEmergencyContactRequest.FullName));
            validator.RuleFor(request => GetRelationship(request)).NotEmpty().WithName(nameof(CreateEmergencyContactRequest.Relationship));
            validator.RuleFor(request => GetPhoneNumber(request)).NotEmpty().Matches(PhonePattern).WithName(nameof(CreateEmergencyContactRequest.PhoneNumber));
            validator.RuleFor(request => GetEmail(request)).NotEmpty().EmailAddress().WithName(nameof(CreateEmergencyContactRequest.Email));
            validator.RuleFor(request => GetPriority(request)).NotNull().GreaterThanOrEqualTo(1).WithName(nameof(CreateEmergencyContactRequest.Priority));
        });
    }

    private static bool IsContinue<TRequest>(TRequest request) where TRequest : class =>
        string.Equals(GetSaveMode(request)?.Trim(), nameof(SaveMode.Continue), StringComparison.OrdinalIgnoreCase);

    private static bool IsAllowedSaveMode(string? saveMode) =>
        string.Equals(saveMode?.Trim(), nameof(SaveMode.Draft), StringComparison.OrdinalIgnoreCase) ||
        string.Equals(saveMode?.Trim(), nameof(SaveMode.Continue), StringComparison.OrdinalIgnoreCase);

    private static string? GetFullName<TRequest>(TRequest request) where TRequest : class => request switch { CreateEmergencyContactRequest create => create.FullName, UpdateEmergencyContactRequest update => update.FullName, _ => null };
    private static string? GetRelationship<TRequest>(TRequest request) where TRequest : class => request switch { CreateEmergencyContactRequest create => create.Relationship, UpdateEmergencyContactRequest update => update.Relationship, _ => null };
    private static string? GetPhoneNumber<TRequest>(TRequest request) where TRequest : class => request switch { CreateEmergencyContactRequest create => create.PhoneNumber, UpdateEmergencyContactRequest update => update.PhoneNumber, _ => null };
    private static string? GetEmail<TRequest>(TRequest request) where TRequest : class => request switch { CreateEmergencyContactRequest create => create.Email, UpdateEmergencyContactRequest update => update.Email, _ => null };
    private static int? GetPriority<TRequest>(TRequest request) where TRequest : class => request switch { CreateEmergencyContactRequest create => create.Priority, UpdateEmergencyContactRequest update => update.Priority, _ => null };
    private static string? GetSaveMode<TRequest>(TRequest request) where TRequest : class => request switch { CreateEmergencyContactRequest create => create.SaveMode, UpdateEmergencyContactRequest update => update.SaveMode, _ => null };
}
