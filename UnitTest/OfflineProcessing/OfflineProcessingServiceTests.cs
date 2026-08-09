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
using MotoSOS.API.Modules.OfflineIngestion.Application;
using MotoSOS.API.Modules.OfflineIngestion.Domain;
using MotoSOS.API.Modules.OfflineProcessing.Application;
using MotoSOS.API.Modules.OfflineProcessing.Contracts;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace UnitTest.OfflineProcessing;

public sealed class OfflineProcessingServiceTests
{
    [Fact]
    public async Task ProcessesSupportedTypesAndSkipsMinorEventWithoutReturningPayload()
    {
        User rider = User(UserRole.Rider); var records = new Records(Record(rider.Id, OfflineIngestionItemType.LocalIncident, IncidentPayload()), Record(rider.Id, OfflineIngestionItemType.AlertDispatchRequest, AlertPayload()), Record(rider.Id, OfflineIngestionItemType.LocationUpdate, LocationPayload()), Record(rider.Id, OfflineIngestionItemType.MinorEvent, "{\"type\":\"bump\"}"));
        OfflineProcessingService service = Service(rider, records);

        RunOfflineProcessingResponse response = await service.RunAsync(rider.Id, new RunOfflineProcessingRequest(20), CancellationToken.None);

        response.Processed.Should().Be(3); response.Skipped.Should().Be(1); response.Failed.Should().Be(0);
        response.Items.Should().Contain(item => item.Type == "minor-event" && item.Status == "Skipped" && item.Reason == "minor_event_processing_not_implemented");
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
    public async Task ProcessesOnlyRiderPendingRecordsAndRoleRulesApply()
    {
        User rider = User(UserRole.Rider); User monitor = User(UserRole.Monitor); var own = Record(rider.Id, OfflineIngestionItemType.MinorEvent, "{\"type\":\"bump\"}"); var other = Record("other", OfflineIngestionItemType.MinorEvent, "{\"type\":\"bump\"}"); var processed = Record(rider.Id, OfflineIngestionItemType.MinorEvent, "{\"type\":\"bump\"}"); processed.ProcessingStatus = OfflineIngestionProcessingStatus.Processed; var records = new Records(own, other, processed);
        (await Service(rider, records).RunAsync(rider.Id, new RunOfflineProcessingRequest(20), CancellationToken.None)).Skipped.Should().Be(1);
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

    private static readonly DateTimeOffset Now = new(2026, 8, 6, 14, 0, 0, TimeSpan.Zero);
    private static OfflineProcessingService Service(User user, Records records, Incidents? incidents = null) => new(new Users(user), records, incidents ?? new Incidents(), new Alerts(), new Locations(), new Clock());
    private static User User(UserRole role) => new() { Email = $"{Guid.NewGuid()}@example.com", Role = role, IsActive = true, FullName = "User" };
    private static OfflineIngestionRecord Record(string userId, OfflineIngestionItemType type, string payload, string? clientEventId = null) => new() { UserId = userId, MobileDeviceId = "mobile", TripId = "trip", BatchId = "batch", ClientEventId = clientEventId ?? Guid.NewGuid().ToString(), Type = type, PayloadVersion = 1, SchemaVersion = 1, IdempotencyKey = Guid.NewGuid().ToString(), AckId = Guid.NewGuid().ToString(), Payload = payload, OccurredAtUtc = Now, ReceivedAtUtc = Now, CreatedAtUtc = Now, ProcessingStatus = OfflineIngestionProcessingStatus.PendingProcessing };
    private static string IncidentPayload() => "{\"clientIncidentId\":\"11111111-1111-1111-1111-111111111111\",\"source\":\"MobileDetection\",\"cause\":\"CountdownTimeout\",\"riskLevel\":\"High\",\"occurredAtUtc\":\"2026-08-06T14:00:00Z\"}";
    private static string IncidentPayloadWithoutClientId() => "{\"source\":\"MobileDetection\",\"cause\":\"CountdownTimeout\",\"riskLevel\":\"High\",\"occurredAtUtc\":\"2026-08-06T14:00:00Z\"}";
    private static string AlertPayload() => "{\"incidentId\":\"incident-1\",\"clientAlertRequestId\":\"22222222-2222-2222-2222-222222222222\",\"priority\":\"High\",\"reason\":\"IncidentCreated\",\"requestedAtUtc\":\"2026-08-06T14:00:00Z\"}";
    private static string LocationPayload() => "{\"incidentId\":\"incident-1\",\"clientLocationUpdateId\":\"33333333-3333-3333-3333-333333333333\",\"latitude\":19,\"longitude\":-99,\"source\":\"MobileApp\",\"recordedAtUtc\":\"2026-08-06T14:00:00Z\"}";
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => Now; }
    private sealed class Users(User user) : IUserRepository { public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult<User?>(user.Id == id ? user : null); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult<User?>(null); public Task AddAsync(User user, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(User user, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Records(params OfflineIngestionRecord[] records) : IOfflineIngestionRepository { public List<OfflineIngestionRecord> Items { get; } = records.ToList(); public Task<OfflineIngestionRecord?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(r => r.IdempotencyKey == key)); public Task<(OfflineIngestionRecord Record, bool IsDuplicate)> AddOrGetDuplicateAsync(OfflineIngestionRecord record, CancellationToken ct) { Items.Add(record); return Task.FromResult((record, false)); } public Task<IReadOnlyList<OfflineIngestionRecord>> ListPendingByUserIdAsync(string userId, int maxItems, CancellationToken ct) => Task.FromResult<IReadOnlyList<OfflineIngestionRecord>>(Items.Where(r => r.UserId == userId && r.ProcessingStatus == OfflineIngestionProcessingStatus.PendingProcessing).Take(maxItems).ToArray()); public Task<OfflineIngestionRecord?> TryMarkProcessingAsync(string id, string userId, DateTimeOffset now, CancellationToken ct) { OfflineIngestionRecord? record = Items.FirstOrDefault(r => r.Id == id && r.UserId == userId && r.ProcessingStatus == OfflineIngestionProcessingStatus.PendingProcessing); if (record is null) return Task.FromResult<OfflineIngestionRecord?>(null); record.ProcessingStatus = OfflineIngestionProcessingStatus.Processing; record.ProcessingStartedAtUtc = now; return Task.FromResult<OfflineIngestionRecord?>(record); } public Task MarkProcessedAsync(string id, string userId, string remoteRecordId, DateTimeOffset now, CancellationToken ct) { OfflineIngestionRecord r = Items.Single(i => i.Id == id); r.ProcessingStatus = OfflineIngestionProcessingStatus.Processed; r.RemoteRecordId = remoteRecordId; r.ProcessedAtUtc = now; return Task.CompletedTask; } public Task MarkIgnoredAsync(string id, string userId, string reason, DateTimeOffset now, CancellationToken ct) { OfflineIngestionRecord r = Items.Single(i => i.Id == id); r.ProcessingStatus = OfflineIngestionProcessingStatus.Ignored; r.ProcessingReason = reason; return Task.CompletedTask; } public Task MarkFailedPermanentAsync(string id, string userId, string errorCode, string errorMessage, DateTimeOffset now, CancellationToken ct) { OfflineIngestionRecord r = Items.Single(i => i.Id == id); r.ProcessingStatus = OfflineIngestionProcessingStatus.FailedPermanent; r.ProcessingErrorCode = errorCode; r.ProcessingErrorMessage = errorMessage; return Task.CompletedTask; } public Task<long> CountByUserIdAndStatusAsync(string userId, OfflineIngestionProcessingStatus status, CancellationToken ct) => Task.FromResult((long)Items.Count(r => r.UserId == userId && r.ProcessingStatus == status)); }
    private sealed class Incidents : IIncidentService { public string? LastClientIncidentId { get; private set; } public Task<CreateIncidentResponse> CreateAsync(string userId, CreateIncidentRequest request, CancellationToken ct) { LastClientIncidentId = request.ClientIncidentId; return Task.FromResult(new CreateIncidentResponse(new IncidentResponse("incident-1", request.TripId!, "vehicle", "mobile", null, request.Source!, request.Cause!, request.RiskLevel!, "Open", request.Score, request.Confidence, request.OccurredAtUtc!.Value, Now, Now, null, null, null, null))); } public Task<GetIncidentsResponse> ListAsync(string userId, string? status, string? tripId, int? pageNumber, int? pageSize, CancellationToken ct) => throw new NotImplementedException(); public Task<GetIncidentResponse> GetAsync(string userId, string incidentId, CancellationToken ct) => throw new NotImplementedException(); public Task<CancelFalsePositiveResponse> CancelFalsePositiveAsync(string userId, string incidentId, CancelFalsePositiveRequest request, CancellationToken ct) => throw new NotImplementedException(); public Task<CloseIncidentResponse> CloseAsync(string userId, string incidentId, CloseIncidentRequest request, CancellationToken ct) => throw new NotImplementedException(); }
    private sealed class Alerts : IAlertDispatchService { public Task<CreateAlertDispatchResponse> CreateAsync(string userId, CreateAlertDispatchRequest request, CancellationToken ct) => Task.FromResult(new CreateAlertDispatchResponse(new AlertDispatchResponse("alert-1", request.IncidentId!, "trip", "vehicle", "mobile", null, request.Priority!, request.Reason!, "PendingDispatch", request.RequestedAtUtc!.Value, Now, Now, null, null, null, 1))); public Task<GetAlertDispatchesResponse> ListAsync(string userId, string? status, string? incidentId, int? pageNumber, int? pageSize, CancellationToken ct) => throw new NotImplementedException(); public Task<GetAlertDispatchResponse> GetAsync(string userId, string id, CancellationToken ct) => throw new NotImplementedException(); public Task<CancelAlertDispatchResponse> CancelAsync(string userId, string id, CancelAlertDispatchRequest request, CancellationToken ct) => throw new NotImplementedException(); }
    private sealed class Locations : ILocationSharingService { public Task<ShareLocationSnapshotResponse> ShareAsync(string userId, ShareLocationSnapshotRequest request, CancellationToken ct) => Task.FromResult(new ShareLocationSnapshotResponse(new LocationSnapshotResponse(request.IncidentId!, "trip", request.Latitude!.Value, request.Longitude!.Value, request.AccuracyMeters, request.Source!, request.RecordedAtUtc!.Value, Now, true, false))); public Task<GetLocationSnapshotResponse> GetForMonitorAsync(string monitorUserId, string notificationDeliveryAttemptId, CancellationToken ct) => throw new NotImplementedException(); public Task<GetLocationSnapshotResponse> GetForRiderAsync(string riderUserId, string incidentId, CancellationToken ct) => throw new NotImplementedException(); }
}
