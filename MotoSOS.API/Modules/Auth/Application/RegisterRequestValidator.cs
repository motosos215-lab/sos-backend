using FluentValidation;
using MotoSOS.API.Modules.Auth.Contracts;

namespace MotoSOS.API.Modules.Auth.Application;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(request => request.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must include an uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must include a lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must include a number.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must include a special character.");

        RuleFor(request => request.ConfirmPassword)
            .NotEmpty()
            .Equal(request => request.Password)
            .WithMessage("Password and confirmPassword must match.");

        RuleFor(request => request.FullName)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(request => request.PhoneNumber)
            .Matches("^[+0-9 ()-]{7,20}$")
            .When(request => !string.IsNullOrWhiteSpace(request.PhoneNumber));

        RuleFor(request => request.AccountType)
            .NotEmpty()
            .Must(BeAllowedPublicAccountType)
            .WithMessage("Account type is not allowed for public registration.");
    }

    private static bool BeAllowedPublicAccountType(string accountType)
    {
        return string.Equals(accountType?.Trim(), "Rider", StringComparison.OrdinalIgnoreCase)
            || string.Equals(accountType?.Trim(), "Conductor", StringComparison.OrdinalIgnoreCase)
            || string.Equals(accountType?.Trim(), "Monitor", StringComparison.OrdinalIgnoreCase);
    }
}
