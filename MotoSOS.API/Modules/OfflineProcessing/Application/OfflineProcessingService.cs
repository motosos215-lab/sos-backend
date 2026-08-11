using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AlertDispatch.Application;
using MotoSOS.API.Modules.AlertDispatch.Contracts;
using MotoSOS.API.Modules.AuditLogs.Application;
using MotoSOS.API.Modules.AuditLogs.Domain;
using MotoSOS.API.Modules.Incidents.Application;
using MotoSOS.API.Modules.Incidents.Contracts;
using MotoSOS.API.Modules.LocationSharing.Application;
using MotoSOS.API.Modules.LocationSharing.Contracts;
using MotoSOS.API.Modules.MinorEvents.Application;
using MotoSOS.API.Modules.MinorEvents.Contracts;
using MotoSOS.API.Modules.OfflineIngestion.Application;
using MotoSOS.API.Modules.OfflineIngestion.Domain;
using MotoSOS.API.Modules.OfflineProcessing.Contracts;
using MotoSOS.API.Modules.OfflineProcessing.Worker;
using MotoSOS.API.Modules.SosAlerts.Application;
using MotoSOS.API.Modules.SosAlerts.Contracts;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Modules.OfflineProcessing.Application;

public sealed class OfflineProcessingService : IOfflineProcessingService
{
    private const int DefaultMaxItems = 20;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IUserRepository _users;
    private readonly IOfflineIngestionRepository _records;
    private readonly IIncidentService _incidents;
    private readonly IAlertDispatchService _alertDispatches;
    private readonly ILocationSharingService _locations;
    private readonly IMinorEventService _minorEvents;
    private readonly ICreateSosAlertService _sosAlerts;
    private readonly IClock _clock;
    private readonly IAuditLogService? _auditLogs;
    private readonly IOfflineProcessingWorkerStateStore? _workerState;
    private readonly IOptions<OfflineProcessingWorkerOptions>? _workerOptions;

    public OfflineProcessingService(IUserRepository users, IOfflineIngestionRepository records, IIncidentService incidents, IAlertDispatchService alertDispatches, ILocationSharingService locations, IMinorEventService minorEvents, ICreateSosAlertService sosAlerts, IClock clock, IAuditLogService? auditLogs = null, IOfflineProcessingWorkerStateStore? workerState = null, IOptions<OfflineProcessingWorkerOptions>? workerOptions = null)
    {
        _users = users;
        _records = records;
        _incidents = incidents;
        _alertDispatches = alertDispatches;
        _locations = locations;
        _minorEvents = minorEvents;
        _sosAlerts = sosAlerts;
        _clock = clock;
        _auditLogs = auditLogs;
        _workerState = workerState;
        _workerOptions = workerOptions;
    }

    public async Task<RunOfflineProcessingResponse> RunAsync(string userId, RunOfflineProcessingRequest request, CancellationToken cancellationToken)
    {
        User rider = await GetRiderAsync(userId, cancellationToken);
        int maxItems = Math.Clamp(request.MaxItems ?? DefaultMaxItems, 1, 100);
        IReadOnlyList<OfflineIngestionRecord> pending = await _records.ListPendingByUserIdAsync(rider.Id, maxItems, cancellationToken);
        var results = new List<OfflineProcessingItemResultResponse>();

        foreach (OfflineIngestionRecord pendingRecord in pending)
        {
            DateTimeOffset now = _clock.UtcNow;
            OfflineIngestionRecord? record = await _records.TryMarkProcessingAsync(pendingRecord.Id, rider.Id, now, cancellationToken);
            if (record is null) continue;
            results.Add(await ProcessRecordAsync(rider.Id, record, cancellationToken));
        }

        var response = new RunOfflineProcessingResponse(
            results.Count(result => result.Status == "Processed"),
            results.Count(result => result.Status == "Skipped"),
            results.Count(result => result.Status == "Failed"),
            results);
        await (_auditLogs?.RecordAsync(rider.Id, rider.Role.ToString(), AuditAction.OfflineProcessingRun, AuditModule.OfflineProcessing, "OfflineProcessingRun", null, AuditOutcome.Success, null, null, null, new Dictionary<string, string> { ["processed"] = response.Processed.ToString(CultureInfo.InvariantCulture), ["skipped"] = response.Skipped.ToString(CultureInfo.InvariantCulture), ["failed"] = response.Failed.ToString(CultureInfo.InvariantCulture), ["maxItems"] = maxItems.ToString(CultureInfo.InvariantCulture) }, cancellationToken) ?? Task.CompletedTask);
        return response;
    }

    public async Task<RunOfflineProcessingResponse> RunWorkerAsync(int maxItems, int recoveryMinutes, CancellationToken cancellationToken)
    {
        int clampedMaxItems = Math.Clamp(maxItems, 1, 100);
        int clampedRecoveryMinutes = Math.Max(1, recoveryMinutes);
        DateTimeOffset now = _clock.UtcNow;
        int recovered = await _records.RecoverStaleProcessingAsync(now.AddMinutes(-clampedRecoveryMinutes), now, clampedMaxItems, cancellationToken);
        IReadOnlyList<OfflineIngestionRecord> pending = await _records.ListPendingAsync(clampedMaxItems, cancellationToken);
        RunOfflineProcessingResponse response = (await ProcessRecordsAsync(pending, clampedMaxItems, cancellationToken)) with { Recovered = recovered };
        await (_auditLogs?.RecordAsync("offline-processing-worker", "System", AuditAction.OfflineProcessingRun, AuditModule.OfflineProcessing, "OfflineProcessingWorker", null, AuditOutcome.Success, null, null, null, new Dictionary<string, string> { ["processed"] = response.Processed.ToString(CultureInfo.InvariantCulture), ["skipped"] = response.Skipped.ToString(CultureInfo.InvariantCulture), ["failed"] = response.Failed.ToString(CultureInfo.InvariantCulture), ["recovered"] = recovered.ToString(CultureInfo.InvariantCulture), ["maxItems"] = clampedMaxItems.ToString(CultureInfo.InvariantCulture), ["recoveryMinutes"] = clampedRecoveryMinutes.ToString(CultureInfo.InvariantCulture), ["runSource"] = "Worker" }, cancellationToken) ?? Task.CompletedTask);
        return response;
    }

    public async Task<GetOfflineProcessingStatusResponse> GetStatusAsync(string userId, CancellationToken cancellationToken)
    {
        User rider = await GetRiderAsync(userId, cancellationToken);
        return new GetOfflineProcessingStatusResponse(
            await _records.CountByUserIdAndStatusAsync(rider.Id, OfflineIngestionProcessingStatus.PendingProcessing, cancellationToken),
            await _records.CountByUserIdAndStatusAsync(rider.Id, OfflineIngestionProcessingStatus.Processing, cancellationToken),
            await _records.CountByUserIdAndStatusAsync(rider.Id, OfflineIngestionProcessingStatus.Processed, cancellationToken),
            await _records.CountByUserIdAndStatusAsync(rider.Id, OfflineIngestionProcessingStatus.FailedPermanent, cancellationToken),
            await _records.CountByUserIdAndStatusAsync(rider.Id, OfflineIngestionProcessingStatus.Ignored, cancellationToken));
    }

    public async Task<OfflineProcessingWorkerStatusResponse> GetWorkerStatusAsync(string adminUserId, CancellationToken cancellationToken)
    {
        User admin = await GetUserAsync(adminUserId, cancellationToken);
        if (admin.Role != UserRole.Admin) throw new ForbiddenAppException("Offline Processing Worker status is available only for admins.");
        OfflineProcessingWorkerOptions options = _workerOptions?.Value ?? new OfflineProcessingWorkerOptions();
        OfflineProcessingWorkerState state = _workerState?.GetSnapshot() ?? new OfflineProcessingWorkerState(false, null, null, null, 0, 0, 0, null, null);
        return new OfflineProcessingWorkerStatusResponse(
            options.Enabled,
            state.IsRunning,
            options.IntervalSeconds,
            options.MaxItemsPerRun,
            options.RunOnStartup,
            options.RecoveryMinutes,
            await _records.CountByStatusAsync(OfflineIngestionProcessingStatus.PendingProcessing, cancellationToken),
            await _records.CountByStatusAsync(OfflineIngestionProcessingStatus.Processing, cancellationToken),
            await _records.CountByStatusAsync(OfflineIngestionProcessingStatus.Processed, cancellationToken),
            await _records.CountByStatusAsync(OfflineIngestionProcessingStatus.FailedPermanent, cancellationToken),
            state.LastRunStartedAtUtc,
            state.LastRunCompletedAtUtc,
            state.LastProcessedCount,
            state.LastFailedCount,
            state.LastRecoveredCount,
            BuildLastError(state));
    }

    private async Task<RunOfflineProcessingResponse> ProcessRecordsAsync(IReadOnlyList<OfflineIngestionRecord> pending, int maxItems, CancellationToken cancellationToken)
    {
        var results = new List<OfflineProcessingItemResultResponse>();
        foreach (OfflineIngestionRecord pendingRecord in pending.Take(maxItems))
        {
            DateTimeOffset now = _clock.UtcNow;
            OfflineIngestionRecord? record = await _records.TryMarkProcessingAsync(pendingRecord.Id, pendingRecord.UserId, now, cancellationToken);
            if (record is null) continue;
            results.Add(await ProcessRecordAsync(record.UserId, record, cancellationToken));
        }

        return new RunOfflineProcessingResponse(
            results.Count(result => result.Status == "Processed"),
            results.Count(result => result.Status == "Skipped"),
            results.Count(result => result.Status == "Failed"),
            results);
    }

    private async Task<OfflineProcessingItemResultResponse> ProcessRecordAsync(string userId, OfflineIngestionRecord record, CancellationToken cancellationToken)
    {
        try
        {
            return record.Type switch
            {
                OfflineIngestionItemType.LocalIncident => await ProcessLocalIncidentAsync(userId, record, cancellationToken),
                OfflineIngestionItemType.AlertDispatchRequest => await ProcessAlertDispatchAsync(userId, record, cancellationToken),
                OfflineIngestionItemType.LocationUpdate => await ProcessLocationUpdateAsync(userId, record, cancellationToken),
                OfflineIngestionItemType.MinorEvent => await ProcessMinorEventAsync(userId, record, cancellationToken),
                OfflineIngestionItemType.OfflineSosAlert => await ProcessOfflineSosAlertAsync(userId, record, cancellationToken),
                _ => await MarkIgnoredAsync(record, "unsupported_offline_record_type", cancellationToken)
            };
        }
        catch (AppException exception)
        {
            return await MarkFailedAsync(record, exception.Code, exception.Message, cancellationToken);
        }
        catch (JsonException)
        {
            return await MarkFailedAsync(record, "invalid_payload", "Offline record payload is invalid.", cancellationToken);
        }
    }

    private async Task<OfflineProcessingItemResultResponse> ProcessLocalIncidentAsync(string userId, OfflineIngestionRecord record, CancellationToken cancellationToken)
    {
        CreateIncidentRequest payload = Deserialize<CreateIncidentRequest>(record.Payload);
        string? clientIncidentId = !string.IsNullOrWhiteSpace(payload.ClientIncidentId) ? payload.ClientIncidentId.Trim() : record.ClientEventId;
        if (!Guid.TryParse(clientIncidentId, out _)) return await MarkFailedAsync(record, "invalid_client_incident_id", "ClientIncidentId must be a valid UUID.", cancellationToken);
        var request = payload with { TripId = string.IsNullOrWhiteSpace(payload.TripId) ? record.TripId : payload.TripId, ClientIncidentId = clientIncidentId };
        CreateIncidentResponse response = await _incidents.CreateAsync(userId, request, cancellationToken);
        return await MarkProcessedAsync(record, response.Incident.Id, cancellationToken);
    }

    private async Task<OfflineProcessingItemResultResponse> ProcessAlertDispatchAsync(string userId, OfflineIngestionRecord record, CancellationToken cancellationToken)
    {
        CreateAlertDispatchRequest request = Deserialize<CreateAlertDispatchRequest>(record.Payload);
        CreateAlertDispatchResponse response = await _alertDispatches.CreateAsync(userId, request, cancellationToken);
        return await MarkProcessedAsync(record, response.AlertDispatch.Id, cancellationToken);
    }

    private async Task<OfflineProcessingItemResultResponse> ProcessLocationUpdateAsync(string userId, OfflineIngestionRecord record, CancellationToken cancellationToken)
    {
        ShareLocationSnapshotRequest request = Deserialize<ShareLocationSnapshotRequest>(record.Payload);
        ShareLocationSnapshotResponse response = await _locations.ShareAsync(userId, request, cancellationToken);
        return await MarkProcessedAsync(record, response.Location.IncidentId, cancellationToken);
    }

    private async Task<OfflineProcessingItemResultResponse> ProcessMinorEventAsync(string userId, OfflineIngestionRecord record, CancellationToken cancellationToken)
    {
        CreateMinorEventRequest payload = Deserialize<CreateMinorEventRequest>(record.Payload);
        if (string.IsNullOrWhiteSpace(payload.EventType) || string.IsNullOrWhiteSpace(payload.Severity) || !payload.Confidence.HasValue)
        {
            throw new ValidationAppException("Minor event payload is invalid.");
        }

        CreateMinorEventResponse response = await _minorEvents.CreateFromOfflineAsync(userId, record.Id, record.TripId, record.ClientEventId, record.MobileDeviceId, record.OccurredAtUtc, payload, cancellationToken);
        return await MarkProcessedAsync(record, response.MinorEvent.Id, cancellationToken);
    }

    private async Task<OfflineProcessingItemResultResponse> ProcessOfflineSosAlertAsync(string userId, OfflineIngestionRecord record, CancellationToken cancellationToken)
    {
        CreateSosAlertRequest payload = Deserialize<CreateSosAlertRequest>(record.Payload);
        var request = payload with
        {
            TripId = string.IsNullOrWhiteSpace(payload.TripId) ? record.TripId : payload.TripId,
            DetectedAtUtc = payload.DetectedAtUtc ?? record.OccurredAtUtc
        };
        CreateSosAlertResponse response = await _sosAlerts.CreateAsync(userId, request, cancellationToken);
        return await MarkProcessedAsync(record, response.Incident.Id, cancellationToken);
    }

    private async Task<OfflineProcessingItemResultResponse> MarkProcessedAsync(OfflineIngestionRecord record, string remoteRecordId, CancellationToken cancellationToken)
    {
        DateTimeOffset now = _clock.UtcNow;
        await _records.MarkProcessedAsync(record.Id, record.UserId, remoteRecordId, now, cancellationToken);
        return new OfflineProcessingItemResultResponse(record.Id, ToContractType(record.Type), "Processed", remoteRecordId);
    }

    private async Task<OfflineProcessingItemResultResponse> MarkIgnoredAsync(OfflineIngestionRecord record, string reason, CancellationToken cancellationToken)
    {
        DateTimeOffset now = _clock.UtcNow;
        await _records.MarkIgnoredAsync(record.Id, record.UserId, reason, now, cancellationToken);
        return new OfflineProcessingItemResultResponse(record.Id, ToContractType(record.Type), "Skipped", null, reason);
    }

    private async Task<OfflineProcessingItemResultResponse> MarkFailedAsync(OfflineIngestionRecord record, string errorCode, string errorMessage, CancellationToken cancellationToken)
    {
        DateTimeOffset now = _clock.UtcNow;
        await _records.MarkFailedPermanentAsync(record.Id, record.UserId, errorCode, errorMessage, now, cancellationToken);
        return new OfflineProcessingItemResultResponse(record.Id, ToContractType(record.Type), "Failed", null, null, errorCode);
    }

    private async Task<User> GetRiderAsync(string userId, CancellationToken cancellationToken)
    {
        User user = await GetUserAsync(userId, cancellationToken);
        if (user.Role != UserRole.Rider) throw new ForbiddenAppException("Offline Processing API is available only for riders.");
        return user;
    }

    private async Task<User> GetUserAsync(string userId, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive) throw new UnauthorizedAppException("Invalid authentication credentials.");
        return user;
    }

    private static T Deserialize<T>(string payload) => JsonSerializer.Deserialize<T>(payload, SerializerOptions) ?? throw new OfflineProcessingFailedAppException("Offline record payload is invalid.");
    private static string? BuildLastError(OfflineProcessingWorkerState state)
    {
        if (string.IsNullOrWhiteSpace(state.LastErrorCode)) return string.IsNullOrWhiteSpace(state.LastErrorMessage) ? null : state.LastErrorMessage;
        if (string.IsNullOrWhiteSpace(state.LastErrorMessage)) return state.LastErrorCode;
        return $"{state.LastErrorCode}: {state.LastErrorMessage}";
    }

    private static string ToContractType(OfflineIngestionItemType type) => type switch
    {
        OfflineIngestionItemType.MinorEvent => "minor-event",
        OfflineIngestionItemType.LocalIncident => "local-incident",
        OfflineIngestionItemType.AlertDispatchRequest => "alert-dispatch-request",
        OfflineIngestionItemType.LocationUpdate => "location-update",
        OfflineIngestionItemType.OfflineSosAlert => "offline-sos-alert",
        _ => type.ToString()
    };
}
