using FluentAssertions;
using Microsoft.Extensions.Options;
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
using MotoSOS.API.Modules.OfflineProcessing.Worker;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace UnitTest.OfflineProcessing;

public sealed class OfflineProcessingWorkerServiceTests
{
    [Fact]
    public async Task WorkerProcessesPendingRecordsGloballyAndRespectsMaxItems()
    {
        var records = new Records(Record("rider-1", OfflineIngestionItemType.MinorEvent, MinorPayload()), Record("rider-2", OfflineIngestionItemType.MinorEvent, MinorPayload()), Record("rider-3", OfflineIngestionItemType.MinorEvent, MinorPayload()));
        OfflineProcessingService service = Service(records);

        RunOfflineProcessingResponse response = await service.RunWorkerAsync(2, 10, CancellationToken.None);

        response.Processed.Should().Be(2);
        records.Items.Count(record => record.ProcessingStatus == OfflineIngestionProcessingStatus.Processed).Should().Be(2);
        records.Items.Count(record => record.ProcessingStatus == OfflineIngestionProcessingStatus.PendingProcessing).Should().Be(1);
    }

    [Fact]
    public async Task RecoveryResetsStaleProcessingAndDoesNotTouchRecentProcessing()
    {
        OfflineIngestionRecord old = Record("rider-1", OfflineIngestionItemType.MinorEvent, MinorPayload());
        old.ProcessingStatus = OfflineIngestionProcessingStatus.Processing;
        old.ProcessingStartedAtUtc = Now.AddMinutes(-20);
        old.UpdatedAtUtc = Now.AddMinutes(-20);
        OfflineIngestionRecord recent = Record("rider-2", OfflineIngestionItemType.MinorEvent, MinorPayload());
        recent.ProcessingStatus = OfflineIngestionProcessingStatus.Processing;
        recent.ProcessingStartedAtUtc = Now.AddMinutes(-2);
        recent.UpdatedAtUtc = Now.AddMinutes(-2);
        var records = new Records(old, recent);

        RunOfflineProcessingResponse response = await Service(records).RunWorkerAsync(20, 10, CancellationToken.None);

        response.Recovered.Should().Be(1);
        old.ProcessingStatus.Should().Be(OfflineIngestionProcessingStatus.Processed);
        recent.ProcessingStatus.Should().Be(OfflineIngestionProcessingStatus.Processing);
    }

    [Fact]
    public async Task RecoveryUsesUpdatedAtWhenProcessingStartedAtIsMissing()
    {
        OfflineIngestionRecord old = Record("rider-1", OfflineIngestionItemType.MinorEvent, MinorPayload());
        old.ProcessingStatus = OfflineIngestionProcessingStatus.Processing;
        old.ProcessingStartedAtUtc = null;
        old.UpdatedAtUtc = Now.AddMinutes(-20);
        var records = new Records(old);

        RunOfflineProcessingResponse response = await Service(records).RunWorkerAsync(20, 10, CancellationToken.None);

        response.Recovered.Should().Be(1);
        old.ProcessingStatus.Should().Be(OfflineIngestionProcessingStatus.Processed);
    }

    [Fact]
    public async Task WorkerStatusRequiresAdminAndReturnsSafeGlobalCounts()
    {
        User admin = new() { Id = "admin", Role = UserRole.Admin, IsActive = true };
        User rider = new() { Id = "rider", Role = UserRole.Rider, IsActive = true };
        var state = new InMemoryOfflineProcessingWorkerStateStore();
        state.MarkStarted(Now.AddMinutes(-1));
        state.MarkSucceeded(Now, 3, 1, 2);
        var records = new Records(Record("rider-1", OfflineIngestionItemType.MinorEvent, MinorPayload()), Record("rider-2", OfflineIngestionItemType.MinorEvent, MinorPayload()));
        records.Items[1].ProcessingStatus = OfflineIngestionProcessingStatus.FailedPermanent;
        OfflineProcessingService service = Service(records, users: new Users(admin, rider), state: state, options: Options.Create(new OfflineProcessingWorkerOptions { Enabled = true, IntervalSeconds = 30, MaxItemsPerRun = 20, RunOnStartup = true, RecoveryMinutes = 10 }));

        OfflineProcessingWorkerStatusResponse status = await service.GetWorkerStatusAsync(admin.Id, CancellationToken.None);

        status.WorkerEnabled.Should().BeTrue();
        status.IntervalSeconds.Should().Be(30);
        status.MaxItemsPerRun.Should().Be(20);
        status.RunOnStartup.Should().BeTrue();
        status.RecoveryMinutes.Should().Be(10);
        status.PendingCount.Should().Be(1);
        status.FailedCount.Should().Be(1);
        status.LastProcessedCount.Should().Be(3);
        status.LastRecoveredCount.Should().Be(2);
        await Assert.ThrowsAsync<ForbiddenAppException>(() => service.GetWorkerStatusAsync(rider.Id, CancellationToken.None));
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
    private static OfflineProcessingService Service(Records records, Users? users = null, InMemoryOfflineProcessingWorkerStateStore? state = null, IOptions<OfflineProcessingWorkerOptions>? options = null) => new(users ?? new Users(), records, new Incidents(), new Alerts(), new Locations(), new MinorEvents(), new Clock(), workerState: state, workerOptions: options);
    private static OfflineIngestionRecord Record(string userId, OfflineIngestionItemType type, string payload) => new() { Id = Guid.NewGuid().ToString("N"), UserId = userId, MobileDeviceId = "mobile", TripId = "trip", BatchId = "batch", ClientEventId = Guid.NewGuid().ToString(), Type = type, PayloadVersion = 1, SchemaVersion = 1, IdempotencyKey = Guid.NewGuid().ToString(), AckId = Guid.NewGuid().ToString(), Payload = payload, OccurredAtUtc = Now, ReceivedAtUtc = Now, CreatedAtUtc = Now, UpdatedAtUtc = Now, ProcessingStatus = OfflineIngestionProcessingStatus.PendingProcessing };
    private static string MinorPayload() => "{\"tripId\":\"trip\",\"clientEventId\":\"11111111-1111-1111-1111-111111111111\",\"eventType\":\"HardBrake\",\"severity\":\"Low\",\"source\":\"OfflineIngestion\",\"confidence\":0.72,\"occurredAtUtc\":\"2026-08-11T12:00:00Z\"}";
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => Now; }
    private sealed class Users(params User[] users) : IUserRepository { private readonly List<User> _users = users.ToList(); public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(_users.FirstOrDefault(user => user.Id == id)); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult<User?>(null); public Task AddAsync(User user, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(User user, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Records(params OfflineIngestionRecord[] records) : IOfflineIngestionRepository
    {
        public List<OfflineIngestionRecord> Items { get; } = records.ToList();
        public Task<OfflineIngestionRecord?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(record => record.IdempotencyKey == key));
        public Task<(OfflineIngestionRecord Record, bool IsDuplicate)> AddOrGetDuplicateAsync(OfflineIngestionRecord record, CancellationToken ct) { Items.Add(record); return Task.FromResult((record, false)); }
        public Task<IReadOnlyList<OfflineIngestionRecord>> ListPendingAsync(int maxItems, CancellationToken ct) => Task.FromResult<IReadOnlyList<OfflineIngestionRecord>>(Items.Where(record => record.ProcessingStatus == OfflineIngestionProcessingStatus.PendingProcessing).OrderBy(record => record.ReceivedAtUtc).Take(maxItems).ToArray());
        public Task<IReadOnlyList<OfflineIngestionRecord>> ListPendingByUserIdAsync(string userId, int maxItems, CancellationToken ct) => Task.FromResult<IReadOnlyList<OfflineIngestionRecord>>(Items.Where(record => record.UserId == userId && record.ProcessingStatus == OfflineIngestionProcessingStatus.PendingProcessing).Take(maxItems).ToArray());
        public Task<OfflineIngestionRecord?> TryMarkProcessingAsync(string id, string userId, DateTimeOffset now, CancellationToken ct) { OfflineIngestionRecord? record = Items.FirstOrDefault(item => item.Id == id && item.UserId == userId && item.ProcessingStatus == OfflineIngestionProcessingStatus.PendingProcessing); if (record is null) return Task.FromResult<OfflineIngestionRecord?>(null); record.ProcessingStatus = OfflineIngestionProcessingStatus.Processing; record.ProcessingStartedAtUtc = now; record.UpdatedAtUtc = now; return Task.FromResult<OfflineIngestionRecord?>(record); }
        public Task<int> RecoverStaleProcessingAsync(DateTimeOffset cutoffUtc, DateTimeOffset now, int maxItems, CancellationToken ct) { OfflineIngestionRecord[] stale = Items.Where(record => record.ProcessingStatus == OfflineIngestionProcessingStatus.Processing && ((record.ProcessingStartedAtUtc.HasValue && record.ProcessingStartedAtUtc <= cutoffUtc) || (!record.ProcessingStartedAtUtc.HasValue && record.UpdatedAtUtc <= cutoffUtc))).Take(maxItems).ToArray(); foreach (OfflineIngestionRecord record in stale) { record.ProcessingStatus = OfflineIngestionProcessingStatus.PendingProcessing; record.UpdatedAtUtc = now; record.ProcessingReason = "recovered_stale_processing"; } return Task.FromResult(stale.Length); }
        public Task MarkProcessedAsync(string id, string userId, string remoteRecordId, DateTimeOffset now, CancellationToken ct) { OfflineIngestionRecord record = Items.Single(item => item.Id == id && item.UserId == userId); record.ProcessingStatus = OfflineIngestionProcessingStatus.Processed; record.RemoteRecordId = remoteRecordId; record.ProcessedAtUtc = now; record.UpdatedAtUtc = now; return Task.CompletedTask; }
        public Task MarkIgnoredAsync(string id, string userId, string reason, DateTimeOffset now, CancellationToken ct) { OfflineIngestionRecord record = Items.Single(item => item.Id == id && item.UserId == userId); record.ProcessingStatus = OfflineIngestionProcessingStatus.Ignored; record.ProcessingReason = reason; record.UpdatedAtUtc = now; return Task.CompletedTask; }
        public Task MarkFailedPermanentAsync(string id, string userId, string errorCode, string errorMessage, DateTimeOffset now, CancellationToken ct) { OfflineIngestionRecord record = Items.Single(item => item.Id == id && item.UserId == userId); record.ProcessingStatus = OfflineIngestionProcessingStatus.FailedPermanent; record.ProcessingErrorCode = errorCode; record.ProcessingErrorMessage = errorMessage; record.UpdatedAtUtc = now; return Task.CompletedTask; }
        public Task<long> CountByStatusAsync(OfflineIngestionProcessingStatus status, CancellationToken ct) => Task.FromResult((long)Items.Count(record => record.ProcessingStatus == status));
        public Task<long> CountByUserIdAndStatusAsync(string userId, OfflineIngestionProcessingStatus status, CancellationToken ct) => Task.FromResult((long)Items.Count(record => record.UserId == userId && record.ProcessingStatus == status));
    }
    private sealed class Incidents : IIncidentService { public Task<CreateIncidentResponse> CreateAsync(string userId, CreateIncidentRequest request, CancellationToken ct) => Task.FromResult(new CreateIncidentResponse(new IncidentResponse("incident-1", request.TripId!, "vehicle", "mobile", null, request.Source!, request.Cause!, request.RiskLevel!, "Open", null, null, request.OccurredAtUtc!.Value, Now, Now, null, null, null, null))); public Task<GetIncidentsResponse> ListAsync(string userId, string? status, string? tripId, int? pageNumber, int? pageSize, CancellationToken ct) => throw new NotImplementedException(); public Task<GetIncidentResponse> GetAsync(string userId, string incidentId, CancellationToken ct) => throw new NotImplementedException(); public Task<CancelFalsePositiveResponse> CancelFalsePositiveAsync(string userId, string incidentId, CancelFalsePositiveRequest request, CancellationToken ct) => throw new NotImplementedException(); public Task<CloseIncidentResponse> CloseAsync(string userId, string incidentId, CloseIncidentRequest request, CancellationToken ct) => throw new NotImplementedException(); }
    private sealed class Alerts : IAlertDispatchService { public Task<CreateAlertDispatchResponse> CreateAsync(string userId, CreateAlertDispatchRequest request, CancellationToken ct) => Task.FromResult(new CreateAlertDispatchResponse(new AlertDispatchResponse("alert-1", request.IncidentId!, "trip", "vehicle", "mobile", null, request.Priority!, request.Reason!, "PendingDispatch", request.RequestedAtUtc!.Value, Now, Now, null, null, null, 1))); public Task<GetAlertDispatchesResponse> ListAsync(string userId, string? status, string? incidentId, int? pageNumber, int? pageSize, CancellationToken ct) => throw new NotImplementedException(); public Task<GetAlertDispatchResponse> GetAsync(string userId, string id, CancellationToken ct) => throw new NotImplementedException(); public Task<CancelAlertDispatchResponse> CancelAsync(string userId, string id, CancelAlertDispatchRequest request, CancellationToken ct) => throw new NotImplementedException(); }
    private sealed class Locations : ILocationSharingService { public Task<ShareLocationSnapshotResponse> ShareAsync(string userId, ShareLocationSnapshotRequest request, CancellationToken ct) => Task.FromResult(new ShareLocationSnapshotResponse(new LocationSnapshotResponse(request.IncidentId!, "trip", request.Latitude!.Value, request.Longitude!.Value, request.AccuracyMeters, request.Source!, request.RecordedAtUtc!.Value, Now, true, false))); public Task<GetLocationSnapshotResponse> GetForMonitorAsync(string monitorUserId, string notificationDeliveryAttemptId, CancellationToken ct) => throw new NotImplementedException(); public Task<GetLocationSnapshotResponse> GetForRiderAsync(string riderUserId, string incidentId, CancellationToken ct) => throw new NotImplementedException(); }
    private sealed class MinorEvents : IMinorEventService { public Task<CreateMinorEventResponse> CreateAsync(string userId, CreateMinorEventRequest request, CancellationToken ct) => throw new NotImplementedException(); public Task<CreateMinorEventResponse> CreateFromOfflineAsync(string userId, string offlineIngestionRecordId, string fallbackTripId, string fallbackClientEventId, string? fallbackMobileDeviceId, DateTimeOffset fallbackOccurredAtUtc, CreateMinorEventRequest request, CancellationToken ct) => Task.FromResult(new CreateMinorEventResponse(new MinorEventResponse($"minor-{offlineIngestionRecordId}", request.TripId ?? fallbackTripId, request.EventType ?? "HardBrake", request.Severity ?? "Low", request.Source ?? "OfflineIngestion", "Recorded", request.Score, request.Confidence ?? 0.72, request.GpsQuality, request.Latitude, request.Longitude, request.SpeedKmh, request.BatteryLevel, request.Message, request.OccurredAtUtc ?? fallbackOccurredAtUtc, Now, Now, Now, offlineIngestionRecordId, new Dictionary<string, string>()))); public Task<GetMinorEventResponse> GetForRiderAsync(string userId, string id, CancellationToken ct) => throw new NotImplementedException(); public Task<GetMinorEventsResponse> ListForRiderAsync(string userId, MinorEventQuery query, CancellationToken ct) => throw new NotImplementedException(); public Task<GetMinorEventsResponse> ListForAdminAsync(string userId, MinorEventQuery query, CancellationToken ct) => throw new NotImplementedException(); public Task<GetMinorEventResponse> MarkReviewedAsync(string userId, string id, CancellationToken ct) => throw new NotImplementedException(); public Task<GetMinorEventResponse> IgnoreAsync(string userId, string id, CancellationToken ct) => throw new NotImplementedException(); }
}
