using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.Onboarding.Contracts;
using MotoSOS.API.Modules.Onboarding.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Modules.Onboarding.Application;

public sealed class OnboardingConfirmationService : IOnboardingConfirmationService
{
    private readonly IUserRepository _users;
    private readonly IOnboardingService _onboarding;
    private readonly IOnboardingConfirmationRepository _confirmations;
    private readonly IClock _clock;

    public OnboardingConfirmationService(
        IUserRepository users,
        IOnboardingService onboarding,
        IOnboardingConfirmationRepository confirmations,
        IClock clock)
    {
        _users = users;
        _onboarding = onboarding;
        _confirmations = confirmations;
        _clock = clock;
    }

    public async Task<ConfirmOnboardingResponse> ConfirmAsync(string userId, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        OnboardingStatusResponse currentStatus = await _onboarding.GetStatusAsync(user.Id, cancellationToken);

        if (!CanConfirm(currentStatus))
        {
            throw new OnboardingNotReadyAppException("Onboarding is not ready to be confirmed.");
        }

        DateTimeOffset now = _clock.UtcNow;
        OnboardingConfirmation? confirmation = await _confirmations.GetByUserIdAsync(user.Id, cancellationToken);
        bool isNew = confirmation is null;

        confirmation ??= new OnboardingConfirmation
        {
            UserId = user.Id,
            ConfirmedAtUtc = now,
            CreatedAtUtc = now
        };

        confirmation.IsOperational = true;
        confirmation.UpdatedAtUtc = now;

        if (isNew)
        {
            await _confirmations.AddAsync(confirmation, cancellationToken);
        }
        else
        {
            await _confirmations.UpdateAsync(confirmation, cancellationToken);
        }

        OnboardingStatusResponse confirmedStatus = await _onboarding.GetStatusAsync(user.Id, cancellationToken);
        return new ConfirmOnboardingResponse(confirmedStatus);
    }

    private async Task<User> GetRiderUserAsync(string userId, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedAppException("Invalid authentication credentials.");
        }

        if (user.Role != UserRole.Rider)
        {
            throw new ForbiddenAppException("This onboarding flow is available only for riders.");
        }

        return user;
    }

    private static bool CanConfirm(OnboardingStatusResponse status) =>
        status.Steps
            .Where(step => step.Key != OnboardingStepKey.Confirmation.ToString())
            .All(step => step.Status == OnboardingStepStatus.Completed.ToString());
}
