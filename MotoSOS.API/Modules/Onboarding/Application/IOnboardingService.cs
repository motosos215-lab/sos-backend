using MotoSOS.API.Modules.Onboarding.Contracts;

namespace MotoSOS.API.Modules.Onboarding.Application;

public interface IOnboardingService
{
    Task<OnboardingStatusResponse> GetStatusAsync(string userId, CancellationToken cancellationToken);
}
