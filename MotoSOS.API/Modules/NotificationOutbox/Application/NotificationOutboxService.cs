using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.NotificationOutbox.Contracts;
using MotoSOS.API.Modules.Notifications.Application;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Modules.NotificationOutbox.Application;

public sealed class NotificationOutboxService : INotificationOutboxService
{
    private const int DefaultMaxItems = 20;
    private const string SimulatedFailureReason = "simulated_failure_requested";

    private readonly IUserRepository _users;
    private readonly INotificationDeliveryAttemptRepository _attempts;
    private readonly IClock _clock;

    public NotificationOutboxService(IUserRepository users, INotificationDeliveryAttemptRepository attempts, IClock clock)
    {
        _users = users; _attempts = attempts; _clock = clock;
    }

    public async Task<RunNotificationOutboxResponse> RunAsync(string adminUserId, RunNotificationOutboxRequest request, CancellationToken cancellationToken)
    {
        await EnsureAdminAsync(adminUserId, cancellationToken);
        int maxItems = request.MaxItems ?? DefaultMaxItems;
        bool simulateFailures = request.SimulateFailures ?? false;
        DateTimeOffset now = _clock.UtcNow;
        IReadOnlyList<NotificationDeliveryAttempt> candidates = await _attempts.ListByStatusAsync(NotificationDeliveryStatus.Prepared, maxItems, cancellationToken);
        var items = new List<NotificationOutboxItemResultResponse>(candidates.Count);
        int sent = 0; int failed = 0; int skipped = 0;
        foreach (NotificationDeliveryAttempt candidate in candidates)
        {
            NotificationDeliveryAttempt? updated = simulateFailures
                ? await _attempts.TryMarkFailedAsync(candidate.Id, SimulatedFailureReason, now, cancellationToken)
                : await _attempts.TryMarkSimulatedSentAsync(candidate.Id, now, cancellationToken);
            if (updated is null)
            {
                skipped++;
                items.Add(new NotificationOutboxItemResultResponse(candidate.Id, candidate.Status.ToString(), candidate.Channel.ToString(), "not_prepared"));
                continue;
            }

            if (updated.Status == NotificationDeliveryStatus.SimulatedSent) sent++;
            if (updated.Status == NotificationDeliveryStatus.Failed) failed++;
            items.Add(new NotificationOutboxItemResultResponse(updated.Id, updated.Status.ToString(), updated.Channel.ToString(), updated.FailureReason));
        }

        return new RunNotificationOutboxResponse(sent + failed, sent, failed, skipped, items);
    }

    public async Task<GetNotificationOutboxStatusResponse> GetStatusAsync(string adminUserId, CancellationToken cancellationToken)
    {
        await EnsureAdminAsync(adminUserId, cancellationToken);
        return new GetNotificationOutboxStatusResponse(
            await _attempts.CountByStatusAsync(NotificationDeliveryStatus.Prepared, cancellationToken),
            await _attempts.CountByStatusAsync(NotificationDeliveryStatus.SimulatedSent, cancellationToken),
            await _attempts.CountByStatusAsync(NotificationDeliveryStatus.Failed, cancellationToken),
            await _attempts.CountByStatusAsync(NotificationDeliveryStatus.Cancelled, cancellationToken));
    }

    public async Task<RetryFailedNotificationOutboxResponse> RetryFailedAsync(string adminUserId, RetryFailedNotificationOutboxRequest request, CancellationToken cancellationToken)
    {
        await EnsureAdminAsync(adminUserId, cancellationToken);
        int maxItems = request.MaxItems ?? DefaultMaxItems;
        DateTimeOffset now = _clock.UtcNow;
        IReadOnlyList<NotificationDeliveryAttempt> candidates = await _attempts.ListByStatusAsync(NotificationDeliveryStatus.Failed, maxItems, cancellationToken);
        var items = new List<RetryFailedNotificationOutboxItemResponse>(candidates.Count);
        foreach (NotificationDeliveryAttempt candidate in candidates)
        {
            NotificationDeliveryAttempt? updated = await _attempts.TryResetFailedToPreparedAsync(candidate.Id, now, cancellationToken);
            if (updated is not null) items.Add(new RetryFailedNotificationOutboxItemResponse(updated.Id, updated.Status.ToString()));
        }

        return new RetryFailedNotificationOutboxResponse(items.Count, items);
    }

    private async Task<User> EnsureAdminAsync(string userId, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive) throw new UnauthorizedAppException("Invalid authentication credentials.");
        if (user.Role != UserRole.Admin) throw new ForbiddenAppException("Notification Outbox API is available only for admins.");
        return user;
    }
}
