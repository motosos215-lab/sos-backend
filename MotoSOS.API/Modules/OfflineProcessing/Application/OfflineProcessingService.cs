using System.Globalization;
using System.Text.Json;
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
    private readonly IClock _clock;
    private readonly IAuditLogService? _auditLogs;

    public OfflineProcessingService(IUserRepository users, IOfflineIngestionRepository records, IIncidentService incidents, IAlertDispatchService alertDispatches, ILocationSharingService locations, IMinorEventService minorEvents, IClock clock, IAuditLogService? auditLogs = null)
    {
        _users = users;
        _records = records;
        _incidents = incidents;
        _alertDispatches = alertDispatches;
        _locations = locations;
        _minorEvents = minorEvents;
        _clock = clock;
        _auditLogs = auditLogs;
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
        User? user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive) throw new UnauthorizedAppException("Invalid authentication credentials.");
        if (user.Role != UserRole.Rider) throw new ForbiddenAppException("Offline Processing API is available only for riders.");
        return user;
    }

    private static T Deserialize<T>(string payload) => JsonSerializer.Deserialize<T>(payload, SerializerOptions) ?? throw new OfflineProcessingFailedAppException("Offline record payload is invalid.");
    private static string ToContractType(OfflineIngestionItemType type) => type switch
    {
        OfflineIngestionItemType.MinorEvent => "minor-event",
        OfflineIngestionItemType.LocalIncident => "local-incident",
        OfflineIngestionItemType.AlertDispatchRequest => "alert-dispatch-request",
        OfflineIngestionItemType.LocationUpdate => "location-update",
        _ => type.ToString()
    };
}
