using MotoSOS.API.Modules.Onboarding.Domain;

namespace MotoSOS.API.Modules.Onboarding.Application;

public interface IOnboardingConfirmationRepository
{
    Task<OnboardingConfirmation?> GetByUserIdAsync(string userId, CancellationToken cancellationToken);
    Task AddAsync(OnboardingConfirmation confirmation, CancellationToken cancellationToken);
    Task UpdateAsync(OnboardingConfirmation confirmation, CancellationToken cancellationToken);
}
