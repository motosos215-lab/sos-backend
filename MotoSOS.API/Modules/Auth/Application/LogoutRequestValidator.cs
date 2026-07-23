using FluentValidation;
using MotoSOS.API.Modules.Auth.Contracts;

namespace MotoSOS.API.Modules.Auth.Application;

public sealed class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator()
    {
        RuleFor(request => request.RefreshToken)
            .NotEmpty();
    }
}
