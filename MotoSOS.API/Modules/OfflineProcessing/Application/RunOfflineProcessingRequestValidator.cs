using FluentValidation;
using MotoSOS.API.Modules.OfflineProcessing.Contracts;

namespace MotoSOS.API.Modules.OfflineProcessing.Application;

public sealed class RunOfflineProcessingRequestValidator : AbstractValidator<RunOfflineProcessingRequest>
{
    public RunOfflineProcessingRequestValidator()
    {
        RuleFor(request => request.MaxItems).InclusiveBetween(1, 100).When(request => request.MaxItems.HasValue);
    }
}
