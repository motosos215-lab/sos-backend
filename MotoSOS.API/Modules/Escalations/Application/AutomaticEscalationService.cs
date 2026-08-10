using System.Globalization;
using Microsoft.Extensions.Options;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AlertAcknowledgements.Application;
using MotoSOS.API.Modules.AlertAcknowledgements.Domain;
using MotoSOS.API.Modules.AlertDispatch.Application;
using MotoSOS.API.Modules.AlertDispatch.Domain;
using MotoSOS.API.Modules.AuditLogs.Application;
using MotoSOS.API.Modules.AuditLogs.Domain;
using MotoSOS.API.Modules.Escalations.Contracts;
using MotoSOS.API.Modules.Escalations.Domain;
using MotoSOS.API.Modules.Escalations.Worker;
using MotoSOS.API.Modules.Incidents.Application;
using MotoSOS.API.Modules.Incidents.Domain;
using MotoSOS.API.Modules.Notifications.Application;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Modules.Escalations.Application;

public sealed class AutomaticEscalationService : IAutomaticEscalationService
{
    private const int DefaultMaxItems = 20;
    private const int DefaultEscalateAfterSeconds = 300;
    private readonly IUserRepository _users;
    private readonly IAlertDispatchRepository _dispatches;
    private readonly IIncidentRepository _incidents;
    private readonly INotificationDeliveryAttemptRepository _attempts;
    private readonly IAlertAcknowledgementRepository _acknowledgements;
    private readonly IEmergencyEscalationRepository _escalations;
    private readonly IEmergencyEscalationIdempotencyKeyFactory _keys;
    private readonly IClock _clock;
    private readonly IAuditLogService? _auditLogs;
    private readonly IAutomaticEscalationWorkerStateStore? _workerState;
    private readonly IOptions<AutomaticEscalationWorkerOptions>? _workerOptions;

    public AutomaticEscalationService(IUserRepository users, IAlertDispatchRepository dispatches, IIncidentRepository incidents, INotificationDeliveryAttemptRepository attempts, IAlertAcknowledgementRepository acknowledgements, IEmergencyEscalationRepository escalations, IEmergencyEscalationIdempotencyKeyFactory keys, IClock clock, IAuditLogService? auditLogs = null, IAutomaticEscalationWorkerStateStore? workerState = null, IOptions<AutomaticEscalationWorkerOptions>? workerOptions = null)
    {
        _users = users; _dispatches = dispatches; _incidents = incidents; _attempts = attempts; _acknowledgements = acknowledgements; _escalations = escalations; _keys = keys; _clock = clock; _auditLogs = auditLogs; _workerState = workerState; _workerOptions = workerOptions;
    }

    public async Task<RunAutomaticEscalationResponse> RunForAdminAsync(string adminUserId, RunAutomaticEscalationRequest request, CancellationToken cancellationToken)
    {
        await EnsureAdminAsync(adminUserId, cancellationToken);
        return await RunAsync(request, "Manual", cancellationToken);
    }

    public async Task<RunAutomaticEscalationResponse> RunAsync(RunAutomaticEscalationRequest request, string runSource, CancellationToken cancellationToken)
    {
        int maxItems = request.MaxItems ?? DefaultMaxItems;
        int escalateAfterSeconds = request.EscalateAfterSeconds ?? DefaultEscalateAfterSeconds;
        DateTimeOffset now = _clock.UtcNow;
        DateTimeOffset dispatchCutoffUtc = now.AddSeconds(-escalateAfterSeconds);
        IReadOnlyList<AlertDispatchRequest> candidates = await _dispatches.ListCandidatesForAutomaticEscalationAsync(dispatchCutoffUtc, maxItems, cancellationToken);
        int processed = 0; int escalated = 0; int skipped = 0; int alreadyEscalated = 0; int alreadyAcknowledged = 0; int notReady = 0; int failed = 0;

        foreach (AlertDispatchRequest dispatch in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            processed++;
            try
            {
                CandidateResult result = await ProcessCandidateAsync(dispatch, escalateAfterSeconds, now, cancellationToken);
                switch (result)
                {
                    case CandidateResult.Escalated: escalated++; break;
                    case CandidateResult.AlreadyEscalated: alreadyEscalated++; skipped++; break;
                    case CandidateResult.AlreadyAcknowledged: alreadyAcknowledged++; skipped++; break;
                    case CandidateResult.NotReady: notReady++; skipped++; break;
                }
            }
            catch
            {
                failed++;
            }
        }

        var response = new RunAutomaticEscalationResponse(processed, escalated, skipped, alreadyEscalated, alreadyAcknowledged, notReady, failed);
        await RecordRunAsync(response, maxItems, escalateAfterSeconds, runSource, cancellationToken);
        return response;
    }

    public async Task<AutomaticEscalationWorkerStatusResponse> GetWorkerStatusAsync(string adminUserId, CancellationToken cancellationToken)
    {
        await EnsureAdminAsync(adminUserId, cancellationToken);
        AutomaticEscalationWorkerState state = _workerState?.GetSnapshot() ?? new AutomaticEscalationWorkerState(false, null, null, null, 0, 0, 0, 0, 0, 0, 0, null, null);
        bool enabled = _workerOptions?.Value.Enabled ?? false;
        return new AutomaticEscalationWorkerStatusResponse(enabled, state.IsRunning, state.LastRunStartedAtUtc, state.LastRunCompletedAtUtc, state.LastRunSucceeded, state.LastRunProcessed, state.LastRunEscalated, state.LastRunSkipped, state.LastRunAlreadyEscalated, state.LastRunAlreadyAcknowledged, state.LastRunNotReady, state.LastRunFailed, state.LastErrorCode, state.LastErrorMessage);
    }

    private async Task<CandidateResult> ProcessCandidateAsync(AlertDispatchRequest dispatch, int escalateAfterSeconds, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (dispatch.Status != AlertDispatchStatus.PendingDispatch) return CandidateResult.NotReady;
        Incident? incident = await _incidents.GetByIdAsync(dispatch.IncidentId, cancellationToken);
        if (incident is null || incident.UserId != dispatch.UserId || incident.Status != IncidentStatus.Open) return CandidateResult.NotReady;
        if (await _escalations.GetByAlertDispatchIdAsync(dispatch.Id, cancellationToken) is not null) return CandidateResult.AlreadyEscalated;

        IReadOnlyList<AlertAcknowledgement> acks = await _acknowledgements.ListByAlertDispatchIdAsync(dispatch.UserId, dispatch.Id, cancellationToken);
        if (acks.Any(a => a.Status == AlertAcknowledgementStatus.Acknowledged)) return CandidateResult.AlreadyAcknowledged;

        IReadOnlyList<NotificationDeliveryAttempt> attempts = await _attempts.ListByAlertDispatchIdAsync(dispatch.UserId, dispatch.Id, cancellationToken);
        if (attempts.Count == 0) return CandidateResult.NotReady;
        DateTimeOffset[] simulatedSentAtUtc = attempts.Where(a => a.Status == NotificationDeliveryStatus.SimulatedSent && a.SimulatedSentAtUtc.HasValue).Select(a => a.SimulatedSentAtUtc!.Value).OrderBy(sentAt => sentAt).ToArray();
        if (simulatedSentAtUtc.Length == 0) return CandidateResult.NotReady;
        if (simulatedSentAtUtc[0].AddSeconds(escalateAfterSeconds) > now) return CandidateResult.NotReady;

        var escalation = new EmergencyEscalation
        {
            UserId = dispatch.UserId,
            IncidentId = incident.Id,
            TripId = dispatch.TripId,
            AlertDispatchId = dispatch.Id,
            Status = EmergencyEscalationStatus.Requested,
            Reason = EmergencyEscalationReason.NoAcknowledgement,
            Level = EmergencyEscalationLevel.Level1,
            RequestedByUserId = "automatic-escalation-worker",
            RequestedByRole = "System",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            IdempotencyKey = _keys.Create(dispatch.UserId, dispatch.Id)
        };
        ApplyCounts(escalation, attempts, acks);
        (EmergencyEscalation saved, bool duplicate) = await _escalations.AddOrGetDuplicateAsync(escalation, cancellationToken);
        if (duplicate) return CandidateResult.AlreadyEscalated;
        await RecordEscalationAsync(saved, cancellationToken);
        return CandidateResult.Escalated;
    }

    private async Task<User> EnsureAdminAsync(string userId, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive) throw new UnauthorizedAppException("Invalid authentication credentials.");
        if (user.Role != UserRole.Admin) throw new ForbiddenAppException("Automatic Escalation Worker API is available only for admins.");
        return user;
    }

    private async Task RecordRunAsync(RunAutomaticEscalationResponse response, int maxItems, int escalateAfterSeconds, string runSource, CancellationToken cancellationToken)
    {
        try
        {
            if (_auditLogs is not null) await _auditLogs.RecordAsync("automatic-escalation-worker", "System", AuditAction.AutomaticEscalationWorkerRun, AuditModule.AlertDispatch, "AutomaticEscalationWorker", null, AuditOutcome.Success, null, null, null, Metadata(response, maxItems, escalateAfterSeconds, runSource), cancellationToken);
        }
        catch
        {
        }
    }

    private async Task RecordEscalationAsync(EmergencyEscalation escalation, CancellationToken cancellationToken)
    {
        try
        {
            if (_auditLogs is not null) await _auditLogs.RecordAsync("automatic-escalation-worker", "System", AuditAction.EmergencyEscalationAutomaticallyRequested, AuditModule.AlertDispatch, "EmergencyEscalation", escalation.Id, AuditOutcome.Success, null, null, null, new Dictionary<string, string> { ["escalationId"] = escalation.Id, ["incidentId"] = escalation.IncidentId, ["alertDispatchId"] = escalation.AlertDispatchId, ["reason"] = escalation.Reason.ToString(), ["level"] = escalation.Level.ToString(), ["runSource"] = "Worker" }, cancellationToken);
        }
        catch
        {
        }
    }

    private static Dictionary<string, string> Metadata(RunAutomaticEscalationResponse response, int maxItems, int escalateAfterSeconds, string runSource) => new()
    {
        ["processed"] = response.Processed.ToString(CultureInfo.InvariantCulture),
        ["escalated"] = response.Escalated.ToString(CultureInfo.InvariantCulture),
        ["skipped"] = response.Skipped.ToString(CultureInfo.InvariantCulture),
        ["alreadyEscalated"] = response.AlreadyEscalated.ToString(CultureInfo.InvariantCulture),
        ["alreadyAcknowledged"] = response.AlreadyAcknowledged.ToString(CultureInfo.InvariantCulture),
        ["notReady"] = response.NotReady.ToString(CultureInfo.InvariantCulture),
        ["failed"] = response.Failed.ToString(CultureInfo.InvariantCulture),
        ["maxItems"] = maxItems.ToString(CultureInfo.InvariantCulture),
        ["escalateAfterSeconds"] = escalateAfterSeconds.ToString(CultureInfo.InvariantCulture),
        ["runSource"] = runSource
    };

    private static void ApplyCounts(EmergencyEscalation e, IReadOnlyList<NotificationDeliveryAttempt> attempts, IReadOnlyList<AlertAcknowledgement> acks)
    {
        e.NotificationsTotal = attempts.Count;
        e.NotificationsPrepared = attempts.Count(a => a.Status == NotificationDeliveryStatus.Prepared);
        e.NotificationsSimulatedSent = attempts.Count(a => a.Status == NotificationDeliveryStatus.SimulatedSent);
        e.NotificationsFailed = attempts.Count(a => a.Status == NotificationDeliveryStatus.Failed);
        e.NotificationsCancelled = attempts.Count(a => a.Status == NotificationDeliveryStatus.Cancelled);
        e.AcknowledgementsTotal = acks.Count;
        e.AcknowledgedCount = acks.Count(a => a.Status == AlertAcknowledgementStatus.Acknowledged);
        e.DeclinedCount = acks.Count(a => a.Status == AlertAcknowledgementStatus.Declined);
        e.ViewedCount = acks.Count(a => a.Status == AlertAcknowledgementStatus.Viewed);
        e.PendingCount = acks.Count(a => a.Status == AlertAcknowledgementStatus.Pending);
    }

    private enum CandidateResult
    {
        Escalated,
        AlreadyEscalated,
        AlreadyAcknowledged,
        NotReady
    }
}
