using MotoSOS.API.Modules.Onboarding.Contracts;

namespace MotoSOS.API.Modules.Onboarding.Application;

public interface IOnboardingConfirmationService
{
    Task<ConfirmOnboardingResponse> ConfirmAsync(string userId, CancellationToken cancellationToken);
}
