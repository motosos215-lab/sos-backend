using FluentValidation;
using MotoSOS.API.Modules.Auth.Contracts;

namespace MotoSOS.API.Modules.Auth.Application;

public sealed class RequestAccessCodeRequestValidator : AbstractValidator<RequestAccessCodeRequest>
{
    public RequestAccessCodeRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty()
            .EmailAddress();
    }
}
