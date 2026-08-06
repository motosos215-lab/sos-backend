using MotoSOS.API.Modules.Onboarding.Contracts;

namespace MotoSOS.API.Modules.Onboarding.Application;

public interface IOnboardingSummaryService
{
    Task<OnboardingSummaryResponse> GetSummaryAsync(string userId, CancellationToken cancellationToken);
}
