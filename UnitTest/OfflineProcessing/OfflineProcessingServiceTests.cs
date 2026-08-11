using System.Text.Json;
using FluentAssertions;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AlertDispatch.Application;
using MotoSOS.API.Modules.AlertDispatch.Contracts;
using MotoSOS.API.Modules.Incidents.Application;
using MotoSOS.API.Modules.Incidents.Contracts;
using MotoSOS.API.Modules.LocationSharing.Application;
using MotoSOS.API.Modules.LocationSharing.Contracts;
using MotoSOS.API.Modules.MinorEvents.Application;
using MotoSOS.API.Modules.MinorEvents.Contracts;
using MotoSOS.API.Modules.OfflineIngestion.Application;
using MotoSOS.API.Modules.OfflineIngestion.Domain;
using MotoSOS.API.Modules.OfflineProcessing.Application;
using MotoSOS.API.Modules.OfflineProcessing.Contracts;
using MotoSOS.API.Modules.SosAlerts.Application;
using MotoSOS.API.Modules.SosAlerts.Contracts;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace UnitTest.OfflineProcessing;

public sealed class OfflineProcessingServiceTests
{
    [Fact]
    public async Task ProcessesSupportedTypesAndMinorEventWithoutReturningPayload()
    {
        User rider = User(UserRole.Rider); var records = new Records(Record(rider.Id, OfflineIngestionItemType.LocalIncident, IncidentPayload()), Record(rider.Id, OfflineIngestionItemType.AlertDispatchRequest, AlertPayload()), Record(rider.Id, OfflineIngestionItemType.LocationUpdate, LocationPayload()), Record(rider.Id, OfflineIngestionItemType.MinorEvent, MinorEventPayload()));
        OfflineProcessingService service = Service(rider, records);

        RunOfflineProcessingResponse response = await service.RunAsync(rider.Id, new RunOfflineProcessingRequest(20), CancellationToken.None);

        response.Processed.Should().Be(4); response.Skipped.Should().Be(0); response.Failed.Should().Be(0);
        response.Items.Should().Contain(item => item.Type == "minor-event" && item.Status == "Processed" && item.RemoteRecordId == "minor-1");
        records.Items.Should().Contain(r => r.Type == OfflineIngestionItemType.LocalIncident && r.RemoteRecordId == "incident-1" && r.ProcessingStatus == OfflineIngestionProcessingStatus.Processed);
        JsonSerializer.Serialize(response).ToLowerInvariant().Should().NotContain("payload");
    }

    [Fact]
    public async Task LocalIncidentFallsBackToClientEventIdAndDoesNotGenerateBackendGuid()
    {
        User rider = User(UserRole.Rider); string clientEventId = Guid.NewGuid().ToString(); var record = Record(rider.Id, OfflineIngestionItemType.LocalIncident, IncidentPayloadWithoutClientId(), clientEventId: clientEventId); var incidents = new Incidents();
        await Service(rider, new Records(record), incidents: incidents).RunAsync(rider.Id, new RunOfflineProcessingRequest(1), CancellationToken.None);
        incidents.LastClientIncidentId.Should().Be(clientEventId);
    }

    [Fact]
    public async Task InvalidFallbackClientIncidentIdMarksFailedPermanent()
    {
        User rider = User(UserRole.Rider); var record = Record(rider.Id, OfflineIngestionItemType.LocalIncident, IncidentPayloadWithoutClientId(), clientEventId: "not-a-guid");
        RunOfflineProcessingResponse response = await Service(rider, new Records(record)).RunAsync(rider.Id, new RunOfflineProcessingRequest(1), CancellationToken.None);
        response.Failed.Should().Be(1); record.ProcessingStatus.Should().Be(OfflineIngestionProcessingStatus.FailedPermanent); record.ProcessingErrorCode.Should().Be("invalid_client_incident_id");
    }

    [Fact]
    public async Task InvalidMinorEventMarksFailedPermanentWithoutBreakingBatch()
    {
        User rider = User(UserRole.Rider); var invalid = Record(rider.Id, OfflineIngestionItemType.MinorEvent, MinorEventPayload()); var valid = Record(rider.Id, OfflineIngestionItemType.LocalIncident, IncidentPayload()); var minorEvents = new MinorEvents { ThrowValidation = true };
        RunOfflineProcessingResponse response = await Service(rider, new Records(invalid, valid), minorEvents: minorEvents).RunAsync(rider.Id, new RunOfflineProcessingRequest(20), CancellationToken.None);
        response.Processed.Should().Be(1); response.Failed.Should().Be(1); invalid.ProcessingStatus.Should().Be(OfflineIngestionProcessingStatus.FailedPermanent); invalid.ProcessingErrorCode.Should().Be("validation_error");
    }

    [Fact]
    public async Task ProcessesOnlyRiderPendingRecordsAndRoleRulesApply()
    {
        User rider = User(UserRole.Rider); User monitor = User(UserRole.Monitor); var own = Record(rider.Id, OfflineIngestionItemType.MinorEvent, MinorEventPayload()); var other = Record("other", OfflineIngestionItemType.MinorEvent, MinorEventPayload()); var processed = Record(rider.Id, OfflineIngestionItemType.MinorEvent, MinorEventPayload()); processed.ProcessingStatus = OfflineIngestionProcessingStatus.Processed; var records = new Records(own, other, processed);
        (await Service(rider, records).RunAsync(rider.Id, new RunOfflineProcessingRequest(20), CancellationToken.None)).Processed.Should().Be(1);
        other.ProcessingStatus.Should().Be(OfflineIngestionProcessingStatus.PendingProcessing); processed.ProcessingStatus.Should().Be(OfflineIngestionProcessingStatus.Processed);
        await Assert.ThrowsAsync<ForbiddenAppException>(() => Service(monitor, records).RunAsync(monitor.Id, new RunOfflineProcessingRequest(1), CancellationToken.None));
    }

    [Fact]
    public async Task StatusCountsAreScopedToRider()
    {
        User rider = User(UserRole.Rider); var pending = Record(rider.Id, OfflineIngestionItemType.MinorEvent, "{}"); var failed = Record(rider.Id, OfflineIngestionItemType.MinorEvent, "{}"); failed.ProcessingStatus = OfflineIngestionProcessingStatus.FailedPermanent; var other = Record("other", OfflineIngestionItemType.MinorEvent, "{}");
        GetOfflineProcessingStatusResponse status = await Service(rider, new Records(pending, failed, other)).GetStatusAsync(rider.Id, CancellationToken.None);
        status.Pending.Should().Be(1); status.Failed.Should().Be(1); status.Processed.Should().Be(0);
    }

    [Fact]
    public async Task OfflineSosAlertCreatesIncidentDispatchAndPreparedAttemptsUsingFallbacks()
    {
        User rider = User(UserRole.Rider); var sos = new SosAlerts(); var record = Record(rider.Id, OfflineIngestionItemType.OfflineSosAlert, SosPayloadWithoutFallbackFields());

        RunOfflineProcessingResponse response = await Service(rider, new Records(record), sosAlerts: sos).RunAsync(rider.Id, new RunOfflineProcessingRequest(1), CancellationToken.None);

        response.Processed.Should().Be(1);
        record.ProcessingStatus.Should().Be(OfflineIngestionProcessingStatus.Processed);
        record.RemoteRecordId.Should().Be("incident-sos-1");
        sos.LastRequest.Should().NotBeNull();
        sos.LastRequest!.TripId.Should().Be(record.TripId);
        sos.LastRequest.DetectedAtUtc.Should().Be(record.OccurredAtUtc);
        sos.LastRequest.ClientIncidentId.Should().Be("22222222-2222-2222-2222-222222222222");
        sos.LastRequest.ClientAlertRequestId.Should().Be("44444444-4444-4444-4444-444444444444");
    }

    [Fact]
    public async Task OfflineSosAlertControlledFailureMarksFailedPermanent()
    {
        User rider = User(UserRole.Rider); var sos = new SosAlerts { ThrowNoChannels = true }; var record = Record(rider.Id, OfflineIngestionItemType.OfflineSosAlert, SosPayloadWithoutFallbackFields());

        RunOfflineProcessingResponse response = await Service(rider, new Records(record), sosAlerts: sos).RunAsync(rider.Id, new RunOfflineProcessingRequest(1), CancellationToken.None);

        response.Failed.Should().Be(1);
        record.ProcessingStatus.Should().Be(OfflineIngestionProcessingStatus.FailedPermanent);
        record.ProcessingErrorCode.Should().Be("notification_not_allowed");
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 6, 14, 0, 0, TimeSpan.Zero);
    private static OfflineProcessingService Service(User user, Records records, Incidents? incidents = null, MinorEvents? minorEvents = null, SosAlerts? sosAlerts = null) => new(new Users(user), records, incidents ?? new Incidents(), new Alerts(), new Locations(), minorEvents ?? new MinorEvents(), sosAlerts ?? new SosAlerts(), new Clock());
    private static User User(UserRole role) => new() { Email = $"{Guid.NewGuid()}@example.com", Role = role, IsActive = true, FullName = "User" };
    private static OfflineIngestionRecord Record(string userId, OfflineIngestionItemType type, string payload, string? clientEventId = null) => new() { UserId = userId, MobileDeviceId = "mobile", TripId = "trip", BatchId = "batch", ClientEventId = clientEventId ?? Guid.NewGuid().ToString(), Type = type, PayloadVersion = 1, SchemaVersion = 1, IdempotencyKey = Guid.NewGuid().ToString(), AckId = Guid.NewGuid().ToString(), Payload = payload, OccurredAtUtc = Now, ReceivedAtUtc = Now, CreatedAtUtc = Now, ProcessingStatus = OfflineIngestionProcessingStatus.PendingProcessing };
    private static string IncidentPayload() => "{\"clientIncidentId\":\"11111111-1111-1111-1111-111111111111\",\"source\":\"MobileDetection\",\"cause\":\"CountdownTimeout\",\"riskLevel\":\"High\",\"occurredAtUtc\":\"2026-08-06T14:00:00Z\"}";
    private static string IncidentPayloadWithoutClientId() => "{\"source\":\"MobileDetection\",\"cause\":\"CountdownTimeout\",\"riskLevel\":\"High\",\"occurredAtUtc\":\"2026-08-06T14:00:00Z\"}";
    private static string AlertPayload() => "{\"incidentId\":\"incident-1\",\"clientAlertRequestId\":\"22222222-2222-2222-2222-222222222222\",\"priority\":\"High\",\"reason\":\"IncidentCreated\",\"requestedAtUtc\":\"2026-08-06T14:00:00Z\"}";
    private static string LocationPayload() => "{\"incidentId\":\"incident-1\",\"clientLocationUpdateId\":\"33333333-3333-3333-3333-333333333333\",\"latitude\":19,\"longitude\":-99,\"source\":\"MobileApp\",\"recordedAtUtc\":\"2026-08-06T14:00:00Z\"}";
    private static string MinorEventPayload() => "{\"tripId\":\"trip\",\"clientEventId\":\"mobile-event-001\",\"eventType\":\"HardBrake\",\"severity\":\"Low\",\"source\":\"OfflineIngestion\",\"score\":42.5,\"confidence\":0.72,\"occurredAtUtc\":\"2026-08-06T14:00:00Z\"}";
    private static string SosPayloadWithoutFallbackFields() => "{\"clientIncidentId\":\"22222222-2222-2222-2222-222222222222\",\"clientAlertRequestId\":\"44444444-4444-4444-4444-444444444444\",\"incidentType\":\"ManualSos\",\"severity\":\"High\",\"latitude\":19.4326,\"longitude\":-99.1332,\"priority\":\"High\",\"reason\":\"ManualSos\",\"notes\":\"SOS creado offline desde Android\"}";
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => Now; }
    private sealed class Users(User user) : IUserRepository { public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult<User?>(user.Id == id ? user : null); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult<User?>(null); public Task AddAsync(User user, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(User user, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Records(params OfflineIngestionRecord[] records) : IOfflineIngestionRepository { public List<OfflineIngestionRecord> Items { get; } = records.ToList(); public Task<OfflineIngestionRecord?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(r => r.IdempotencyKey == key)); public Task<(OfflineIngestionRecord Record, bool IsDuplicate)> AddOrGetDuplicateAsync(OfflineIngestionRecord record, CancellationToken ct) { Items.Add(record); return Task.FromResult((record, false)); } public Task<IReadOnlyList<OfflineIngestionRecord>> ListPendingByUserIdAsync(string userId, int maxItems, CancellationToken ct) => Task.FromResult<IReadOnlyList<OfflineIngestionRecord>>(Items.Where(r => r.UserId == userId && r.ProcessingStatus == OfflineIngestionProcessingStatus.PendingProcessing).Take(maxItems).ToArray()); public Task<OfflineIngestionRecord?> TryMarkProcessingAsync(string id, string userId, DateTimeOffset now, CancellationToken ct) { OfflineIngestionRecord? record = Items.FirstOrDefault(r => r.Id == id && r.UserId == userId && r.ProcessingStatus == OfflineIngestionProcessingStatus.PendingProcessing); if (record is null) return Task.FromResult<OfflineIngestionRecord?>(null); record.ProcessingStatus = OfflineIngestionProcessingStatus.Processing; record.ProcessingStartedAtUtc = now; return Task.FromResult<OfflineIngestionRecord?>(record); } public Task MarkProcessedAsync(string id, string userId, string remoteRecordId, DateTimeOffset now, CancellationToken ct) { OfflineIngestionRecord r = Items.Single(i => i.Id == id); r.ProcessingStatus = OfflineIngestionProcessingStatus.Processed; r.RemoteRecordId = remoteRecordId; r.ProcessedAtUtc = now; return Task.CompletedTask; } public Task MarkIgnoredAsync(string id, string userId, string reason, DateTimeOffset now, CancellationToken ct) { OfflineIngestionRecord r = Items.Single(i => i.Id == id); r.ProcessingStatus = OfflineIngestionProcessingStatus.Ignored; r.ProcessingReason = reason; return Task.CompletedTask; } public Task MarkFailedPermanentAsync(string id, string userId, string errorCode, string errorMessage, DateTimeOffset now, CancellationToken ct) { OfflineIngestionRecord r = Items.Single(i => i.Id == id); r.ProcessingStatus = OfflineIngestionProcessingStatus.FailedPermanent; r.ProcessingErrorCode = errorCode; r.ProcessingErrorMessage = errorMessage; return Task.CompletedTask; } public Task<long> CountByUserIdAndStatusAsync(string userId, OfflineIngestionProcessingStatus status, CancellationToken ct) => Task.FromResult((long)Items.Count(r => r.UserId == userId && r.ProcessingStatus == status)); }
    private sealed class Incidents : IIncidentService { public string? LastClientIncidentId { get; private set; } public Task<CreateIncidentResponse> CreateAsync(string userId, CreateIncidentRequest request, CancellationToken ct) { LastClientIncidentId = request.ClientIncidentId; return Task.FromResult(new CreateIncidentResponse(new IncidentResponse("incident-1", request.TripId!, "vehicle", "mobile", null, request.Source!, request.Cause!, request.RiskLevel!, "Open", request.Score, request.Confidence, request.OccurredAtUtc!.Value, Now, Now, null, null, null, null))); } public Task<GetIncidentsResponse> ListAsync(string userId, string? status, string? tripId, int? pageNumber, int? pageSize, CancellationToken ct) => throw new NotImplementedException(); public Task<GetIncidentResponse> GetAsync(string userId, string incidentId, CancellationToken ct) => throw new NotImplementedException(); public Task<CancelFalsePositiveResponse> CancelFalsePositiveAsync(string userId, string incidentId, CancelFalsePositiveRequest request, CancellationToken ct) => throw new NotImplementedException(); public Task<CloseIncidentResponse> CloseAsync(string userId, string incidentId, CloseIncidentRequest request, CancellationToken ct) => throw new NotImplementedException(); }
    private sealed class Alerts : IAlertDispatchService { public Task<CreateAlertDispatchResponse> CreateAsync(string userId, CreateAlertDispatchRequest request, CancellationToken ct) => Task.FromResult(new CreateAlertDispatchResponse(new AlertDispatchResponse("alert-1", request.IncidentId!, "trip", "vehicle", "mobile", null, request.Priority!, request.Reason!, "PendingDispatch", request.RequestedAtUtc!.Value, Now, Now, null, null, null, 1))); public Task<GetAlertDispatchesResponse> ListAsync(string userId, string? status, string? incidentId, int? pageNumber, int? pageSize, CancellationToken ct) => throw new NotImplementedException(); public Task<GetAlertDispatchResponse> GetAsync(string userId, string id, CancellationToken ct) => throw new NotImplementedException(); public Task<CancelAlertDispatchResponse> CancelAsync(string userId, string id, CancelAlertDispatchRequest request, CancellationToken ct) => throw new NotImplementedException(); }
    private sealed class Locations : ILocationSharingService { public Task<ShareLocationSnapshotResponse> ShareAsync(string userId, ShareLocationSnapshotRequest request, CancellationToken ct) => Task.FromResult(new ShareLocationSnapshotResponse(new LocationSnapshotResponse(request.IncidentId!, "trip", request.Latitude!.Value, request.Longitude!.Value, request.AccuracyMeters, request.Source!, request.RecordedAtUtc!.Value, Now, true, false))); public Task<GetLocationSnapshotResponse> GetForMonitorAsync(string monitorUserId, string notificationDeliveryAttemptId, CancellationToken ct) => throw new NotImplementedException(); public Task<GetLocationSnapshotResponse> GetForRiderAsync(string riderUserId, string incidentId, CancellationToken ct) => throw new NotImplementedException(); }
    private sealed class MinorEvents : IMinorEventService { public bool ThrowValidation { get; init; } public Task<CreateMinorEventResponse> CreateAsync(string userId, CreateMinorEventRequest request, CancellationToken ct) => throw new NotImplementedException(); public Task<CreateMinorEventResponse> CreateFromOfflineAsync(string userId, string offlineIngestionRecordId, string fallbackTripId, string fallbackClientEventId, string? fallbackMobileDeviceId, DateTimeOffset fallbackOccurredAtUtc, CreateMinorEventRequest request, CancellationToken ct) { if (ThrowValidation) throw new ValidationAppException("Minor event payload is invalid."); return Task.FromResult(new CreateMinorEventResponse(new MinorEventResponse("minor-1", request.TripId ?? fallbackTripId, request.EventType ?? "HardBrake", request.Severity ?? "Low", request.Source ?? "OfflineIngestion", "Recorded", request.Score, request.Confidence ?? 0.72, request.GpsQuality, request.Latitude, request.Longitude, request.SpeedKmh, request.BatteryLevel, request.Message, request.OccurredAtUtc ?? fallbackOccurredAtUtc, Now, Now, Now, offlineIngestionRecordId, new Dictionary<string, string>()))); } public Task<GetMinorEventResponse> GetForRiderAsync(string userId, string id, CancellationToken ct) => throw new NotImplementedException(); public Task<GetMinorEventsResponse> ListForRiderAsync(string userId, MinorEventQuery query, CancellationToken ct) => throw new NotImplementedException(); public Task<GetMinorEventsResponse> ListForAdminAsync(string userId, MinorEventQuery query, CancellationToken ct) => throw new NotImplementedException(); public Task<GetMinorEventResponse> MarkReviewedAsync(string userId, string id, CancellationToken ct) => throw new NotImplementedException(); public Task<GetMinorEventResponse> IgnoreAsync(string userId, string id, CancellationToken ct) => throw new NotImplementedException(); }
    private sealed class SosAlerts : ICreateSosAlertService { public CreateSosAlertRequest? LastRequest { get; private set; } public bool ThrowNoChannels { get; init; } public Task<CreateSosAlertResponse> CreateAsync(string userId, CreateSosAlertRequest request, CancellationToken ct) { LastRequest = request; if (ThrowNoChannels) throw new NotificationNotAllowedAppException("No notification channel is available."); return Task.FromResult(new CreateSosAlertResponse(new SosAlertIncidentResponse("incident-sos-1", request.TripId!, "Open", request.IncidentType!, request.Severity!), new SosAlertDispatchResponse("alert-sos-1", "incident-sos-1", "PendingDispatch", 1), [new SosAlertNotificationAttemptResponse("attempt-1", "Push", "Prepared", "None", "contact-1", "Monitor")], new SosAlertSummaryResponse(1, 0, 0, 1))); } }
}
