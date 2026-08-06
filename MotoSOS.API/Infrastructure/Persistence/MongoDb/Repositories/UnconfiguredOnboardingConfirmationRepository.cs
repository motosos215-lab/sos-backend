using MotoSOS.API.Modules.Onboarding.Application;
using MotoSOS.API.Modules.Onboarding.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class UnconfiguredOnboardingConfirmationRepository : IOnboardingConfirmationRepository
{
    public Task<OnboardingConfirmation?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) => throw CreateException();
    public Task AddAsync(OnboardingConfirmation confirmation, CancellationToken cancellationToken) => throw CreateException();
    public Task UpdateAsync(OnboardingConfirmation confirmation, CancellationToken cancellationToken) => throw CreateException();
    private static InvalidOperationException CreateException() => new("MongoDB is not configured.");
}
