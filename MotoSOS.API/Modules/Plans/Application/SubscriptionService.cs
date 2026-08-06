using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.Plans.Contracts;
using MotoSOS.API.Modules.Plans.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Modules.Plans.Application;

public sealed class SubscriptionService : ISubscriptionService
{
    private readonly IUserRepository _users;
    private readonly IUserSubscriptionRepository _subscriptions;
    private readonly IPlanCatalogService _planCatalog;
    private readonly IClock _clock;

    public SubscriptionService(IUserRepository users, IUserSubscriptionRepository subscriptions, IPlanCatalogService planCatalog, IClock clock)
    {
        _users = users;
        _subscriptions = subscriptions;
        _planCatalog = planCatalog;
        _clock = clock;
    }

    public async Task<GetMySubscriptionResponse> GetMySubscriptionAsync(string userId, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        UserSubscription? subscription = await _subscriptions.GetByUserIdAsync(user.Id, cancellationToken);

        return subscription is null
            ? new GetMySubscriptionResponse(null, _planCatalog.ToResponse(_planCatalog.GetDefaultPlan()))
            : new GetMySubscriptionResponse(ToResponse(subscription), null);
    }

    public async Task<SelectBasicSubscriptionResponse> SelectBasicAsync(string userId, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        DateTimeOffset now = _clock.UtcNow;
        UserSubscription? subscription = await _subscriptions.GetByUserIdAsync(user.Id, cancellationToken);
        bool isNew = subscription is null;

        subscription ??= new UserSubscription
        {
            UserId = user.Id,
            CreatedAtUtc = now,
            StartedAtUtc = now
        };

        subscription.PlanTier = PlanTier.Basic;
        subscription.Status = SubscriptionStatus.Active;
        subscription.Source = SubscriptionSource.WebBasic;
        subscription.StartedAtUtc = subscription.StartedAtUtc == default ? now : subscription.StartedAtUtc;
        subscription.ExpiresAtUtc = null;
        subscription.CancelledAtUtc = null;
        subscription.ConfirmedAtUtc ??= now;
        subscription.UpdatedAtUtc = now;

        if (isNew)
        {
            await _subscriptions.AddAsync(subscription, cancellationToken);
        }
        else
        {
            await _subscriptions.UpdateAsync(subscription, cancellationToken);
        }

        return new SelectBasicSubscriptionResponse(ToResponse(subscription));
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
            throw new ForbiddenAppException("This subscriptions flow is available only for riders.");
        }

        return user;
    }

    private static SubscriptionResponse ToResponse(UserSubscription subscription) => new(
        subscription.Id,
        subscription.UserId,
        subscription.PlanTier.ToString(),
        subscription.Status.ToString(),
        subscription.Source.ToString(),
        subscription.StartedAtUtc,
        subscription.ExpiresAtUtc,
        subscription.CancelledAtUtc,
        subscription.ConfirmedAtUtc,
        subscription.CreatedAtUtc,
        subscription.UpdatedAtUtc);
}
