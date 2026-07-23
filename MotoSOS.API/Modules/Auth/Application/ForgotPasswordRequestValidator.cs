using FluentValidation;
using MotoSOS.API.Modules.Auth.Contracts;

namespace MotoSOS.API.Modules.Auth.Application;

public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty()
            .EmailAddress();
    }
}
