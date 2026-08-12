using System.Globalization;
using Microsoft.Extensions.Options;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AuditLogs.Application;
using MotoSOS.API.Modules.AuditLogs.Domain;
using MotoSOS.API.Modules.NotificationOutbox.Contracts;
using MotoSOS.API.Modules.NotificationOutbox.Worker;
using MotoSOS.API.Modules.Notifications.Application;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.Notifications.Providers;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Modules.NotificationOutbox.Application;

public sealed class NotificationOutboxService : INotificationOutboxService
{
    private const int DefaultMaxItems = 20;
    private const string SimulatedFailureReason = "simulated_failure_requested";
    private const string ProviderExceptionReason = "simulated_provider_failure";
    private const string UnsupportedChannelReason = "unsupported_notification_channel";

    private readonly IUserRepository _users;
    private readonly INotificationDeliveryAttemptRepository _attempts;
    private readonly INotificationProviderResolver _providers;
    private readonly IClock _clock;
    private readonly IAuditLogService? _auditLogs;
    private readonly INotificationOutboxWorkerStateStore? _workerState;
    private readonly IOptions<NotificationOutboxWorkerOptions>? _workerOptions;

    public NotificationOutboxService(IUserRepository users, INotificationDeliveryAttemptRepository attempts, INotificationProviderResolver providers, IClock clock, IAuditLogService? auditLogs = null, INotificationOutboxWorkerStateStore? workerState = null, IOptions<NotificationOutboxWorkerOptions>? workerOptions = null)
    {
        _users = users; _attempts = attempts; _providers = providers; _clock = clock; _auditLogs = auditLogs; _workerState = workerState; _workerOptions = workerOptions;
    }

    public async Task<RunNotificationOutboxResponse> RunAsync(string adminUserId, RunNotificationOutboxRequest request, CancellationToken cancellationToken)
    {
        User user = await EnsureAdminAsync(adminUserId, cancellationToken);
        return await ProcessAsync(user.Id, user.Role.ToString(), request, AuditAction.NotificationOutboxRun, null, null, cancellationToken);
    }

    public async Task<RunNotificationOutboxResponse> RunWorkerAsync(RunNotificationOutboxRequest request, int intervalSeconds, CancellationToken cancellationToken)
    {
        return await ProcessAsync("notification-outbox-worker", "System", request, AuditAction.NotificationOutboxWorkerRun, "Worker", intervalSeconds, cancellationToken);
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

    public async Task<NotificationOutboxWorkerStatusResponse> GetWorkerStatusAsync(string adminUserId, CancellationToken cancellationToken)
    {
        await EnsureAdminAsync(adminUserId, cancellationToken);
        NotificationOutboxWorkerState state = _workerState?.GetSnapshot() ?? new NotificationOutboxWorkerState(false, null, null, null, 0, 0, 0, 0, null, null);
        NotificationOutboxWorkerOptions options = _workerOptions?.Value ?? new NotificationOutboxWorkerOptions();
        return new NotificationOutboxWorkerStatusResponse(
            options.Enabled,
            state.IsRunning,
            options.IntervalSeconds,
            options.MaxItemsPerRun,
            options.SimulateFailures,
            options.RunOnStartup,
            state.LastRunStartedAtUtc,
            state.LastRunCompletedAtUtc,
            state.LastRunProcessed,
            state.LastRunFailed,
            BuildLastError(state));
    }

    public async Task<RetryFailedNotificationOutboxResponse> RetryFailedAsync(string adminUserId, RetryFailedNotificationOutboxRequest request, CancellationToken cancellationToken)
    {
        User user = await EnsureAdminAsync(adminUserId, cancellationToken);
        int maxItems = request.MaxItems ?? DefaultMaxItems;
        DateTimeOffset now = _clock.UtcNow;
        IReadOnlyList<NotificationDeliveryAttempt> candidates = await _attempts.ListByStatusAsync(NotificationDeliveryStatus.Failed, maxItems, cancellationToken);
        var items = new List<RetryFailedNotificationOutboxItemResponse>(candidates.Count);
        foreach (NotificationDeliveryAttempt candidate in candidates)
        {
            NotificationDeliveryAttempt? updated = await _attempts.TryResetFailedToPreparedAsync(candidate.Id, now, cancellationToken);
            if (updated is not null) items.Add(new RetryFailedNotificationOutboxItemResponse(updated.Id, updated.Status.ToString()));
        }

        var response = new RetryFailedNotificationOutboxResponse(items.Count, items);
        await RecordAsync(user.Id, user.Role.ToString(), AuditAction.NotificationOutboxRetryFailed, "NotificationOutbox", null, "retry-failed", new Dictionary<string, string> { ["retried"] = response.Retried.ToString(CultureInfo.InvariantCulture), ["maxItems"] = maxItems.ToString(CultureInfo.InvariantCulture) }, cancellationToken);
        return response;
    }

    private async Task<RunNotificationOutboxResponse> ProcessAsync(string actorUserId, string actorRole, RunNotificationOutboxRequest request, AuditAction auditAction, string? runSource, int? intervalSeconds, CancellationToken cancellationToken)
    {
        int maxItems = request.MaxItems ?? DefaultMaxItems;
        bool simulateFailures = request.SimulateFailures ?? false;
        DateTimeOffset now = _clock.UtcNow;
        IReadOnlyList<NotificationDeliveryAttempt> candidates = await _attempts.ListByStatusAsync(NotificationDeliveryStatus.Prepared, maxItems, cancellationToken);
        var items = new List<NotificationOutboxItemResultResponse>(candidates.Count);
        int sent = 0; int failed = 0; int skipped = 0;
        foreach (NotificationDeliveryAttempt candidate in candidates)
        {
            NotificationProviderResult result = await SendViaProviderAsync(candidate, simulateFailures, now, cancellationToken);
            NotificationDeliveryAttempt? updated = result.DeliveryStatus == NotificationProviderDeliveryStatus.Sent
                ? await _attempts.TryMarkSentAsync(candidate.Id, MapProvider(result.ProviderType), result.ProviderMessageId, result.SentAtUtc ?? now, cancellationToken)
                : await _attempts.TryMarkFailedAsync(candidate.Id, MapProvider(result.ProviderType), NormalizeFailureReason(result.ErrorCode), result.FailedAtUtc ?? now, cancellationToken);
            if (updated is null)
            {
                skipped++;
                items.Add(new NotificationOutboxItemResultResponse(candidate.Id, candidate.Status.ToString(), candidate.Channel.ToString(), "not_prepared"));
                continue;
            }

            if (updated.Status == NotificationDeliveryStatus.SimulatedSent) sent++;
            if (updated.Status == NotificationDeliveryStatus.Failed) failed++;
            await RecordProviderAsync(actorUserId, actorRole, updated, result, cancellationToken);
            items.Add(new NotificationOutboxItemResultResponse(updated.Id, updated.Status.ToString(), updated.Channel.ToString(), updated.FailureReason));
        }

        var response = new RunNotificationOutboxResponse(sent + failed, sent, failed, skipped, items);
        var metadata = new Dictionary<string, string> { ["processed"] = response.Processed.ToString(CultureInfo.InvariantCulture), ["simulatedSent"] = response.SimulatedSent.ToString(CultureInfo.InvariantCulture), ["failed"] = response.Failed.ToString(CultureInfo.InvariantCulture), ["skipped"] = response.Skipped.ToString(CultureInfo.InvariantCulture), ["simulateFailures"] = simulateFailures.ToString(), ["maxItems"] = maxItems.ToString(CultureInfo.InvariantCulture) };
        if (!string.IsNullOrWhiteSpace(runSource)) metadata["runSource"] = runSource;
        if (intervalSeconds.HasValue) metadata["intervalSeconds"] = intervalSeconds.Value.ToString(CultureInfo.InvariantCulture);
        await RecordAsync(actorUserId, actorRole, auditAction, "NotificationOutbox", null, runSource is null ? "run" : "worker-run", metadata, cancellationToken);
        return response;
    }

    private async Task<User> EnsureAdminAsync(string userId, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive) throw new UnauthorizedAppException("Invalid authentication credentials.");
        if (user.Role != UserRole.Admin) throw new ForbiddenAppException("Notification Outbox API is available only for admins.");
        return user;
    }

    private async Task<NotificationProviderResult> SendViaProviderAsync(NotificationDeliveryAttempt attempt, bool simulateFailures, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!TryMapChannel(attempt.Channel, out NotificationProviderChannel channel))
        {
            return new NotificationProviderResult(NotificationProviderType.Simulated, NotificationProviderChannel.Sms, NotificationProviderDeliveryStatus.Failed, null, "unsupported-channel", UnsupportedChannelReason, "Notification channel is not supported.", null, now);
        }

        try
        {
            INotificationProvider provider = _providers.Resolve(channel);
            return await provider.SendAsync(new NotificationProviderRequest(attempt.Id, attempt.AlertDispatchId, attempt.IncidentId, channel, simulateFailures, attempt.ContactEmail, attempt.ContactPhoneNumber), cancellationToken);
        }
        catch (Exception)
        {
            return new NotificationProviderResult(NotificationProviderType.Simulated, channel, NotificationProviderDeliveryStatus.Failed, null, "provider-exception", ProviderExceptionReason, "Notification provider failed in a controlled way.", null, now);
        }
    }

    private async Task RecordProviderAsync(string userId, string role, NotificationDeliveryAttempt attempt, NotificationProviderResult result, CancellationToken cancellationToken)
    {
        AuditAction action = result.ProviderType == NotificationProviderType.Fcm
            ? result.DeliveryStatus == NotificationProviderDeliveryStatus.Sent ? AuditAction.NotificationProviderFcmSent : AuditAction.NotificationProviderFcmFailed
            : result.ProviderType == NotificationProviderType.Email
            ? result.DeliveryStatus == NotificationProviderDeliveryStatus.Sent ? AuditAction.NotificationProviderEmailSent : AuditAction.NotificationProviderEmailFailed
            : result.ProviderType == NotificationProviderType.Sms
            ? result.DeliveryStatus == NotificationProviderDeliveryStatus.Sent ? AuditAction.NotificationProviderSmsSent : AuditAction.NotificationProviderSmsFailed
            : result.DeliveryStatus == NotificationProviderDeliveryStatus.Sent ? AuditAction.NotificationProviderSimulatedSent : AuditAction.NotificationProviderSimulatedFailed;
        IReadOnlyDictionary<string, string> metadata = result.ProviderType == NotificationProviderType.Fcm || result.ProviderType == NotificationProviderType.Email || result.ProviderType == NotificationProviderType.Sms
            ? new Dictionary<string, string> { ["notificationDeliveryAttemptId"] = attempt.Id, ["provider"] = result.ProviderType.ToString(), ["channel"] = attempt.Channel.ToString(), ["status"] = result.DeliveryStatus.ToString(), ["failureCode"] = result.ErrorCode ?? string.Empty }
            : new Dictionary<string, string> { ["notificationDeliveryAttemptId"] = attempt.Id, ["alertDispatchId"] = attempt.AlertDispatchId, ["incidentId"] = attempt.IncidentId, ["channel"] = attempt.Channel.ToString(), ["providerType"] = result.ProviderType.ToString(), ["deliveryStatus"] = result.DeliveryStatus.ToString(), ["providerMessageId"] = result.ProviderMessageId ?? string.Empty, ["errorCode"] = result.ErrorCode ?? string.Empty };
        await RecordAsync(userId, role, action, "NotificationDeliveryAttempt", attempt.Id, result.ErrorCode, metadata, cancellationToken);
    }

    private async Task RecordAsync(string userId, string role, AuditAction action, string entityType, string? entityId, string? reason, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken)
    {
        try
        {
            if (_auditLogs is not null) await _auditLogs.RecordAsync(userId, role, action, AuditModule.NotificationOutbox, entityType, entityId, AuditOutcome.Success, reason, null, null, metadata, cancellationToken);
        }
        catch
        {
        }
    }

    private static string NormalizeFailureReason(string? errorCode) => string.IsNullOrWhiteSpace(errorCode) ? SimulatedFailureReason : errorCode.Trim();
    private static string? BuildLastError(NotificationOutboxWorkerState state)
    {
        if (string.IsNullOrWhiteSpace(state.LastErrorCode)) return string.IsNullOrWhiteSpace(state.LastErrorMessage) ? null : state.LastErrorMessage;
        if (string.IsNullOrWhiteSpace(state.LastErrorMessage)) return state.LastErrorCode;
        return $"{state.LastErrorCode}: {state.LastErrorMessage}";
    }

    private static NotificationProvider MapProvider(NotificationProviderType providerType) => providerType switch
    {
        NotificationProviderType.Fcm => NotificationProvider.Fcm,
        NotificationProviderType.Email => NotificationProvider.Email,
        NotificationProviderType.Sms => NotificationProvider.Sms,
        _ => NotificationProvider.Simulated
    };
    private static bool TryMapChannel(NotificationChannel channel, out NotificationProviderChannel providerChannel)
    {
        providerChannel = channel switch
        {
            NotificationChannel.Sms => NotificationProviderChannel.Sms,
            NotificationChannel.Email => NotificationProviderChannel.Email,
            NotificationChannel.Push => NotificationProviderChannel.Push,
            _ => default
        };
        return providerChannel != default;
    }
}
