using FluentAssertions;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AuditLogs.Application;
using MotoSOS.API.Modules.AuditLogs.Contracts;
using MotoSOS.API.Modules.AuditLogs.Domain;
using MotoSOS.API.Modules.MinorEvents.Application;
using MotoSOS.API.Modules.MinorEvents.Domain;
using MotoSOS.API.Modules.TelemetrySummary.Application;
using MotoSOS.API.Modules.TelemetrySummary.Contracts;
using MotoSOS.API.Modules.TelemetrySummary.Domain;
using MotoSOS.API.Modules.Trips.Application;
using MotoSOS.API.Modules.Trips.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace UnitTest.TelemetrySummary;

public sealed class TelemetrySummaryServiceTests
{
    [Fact]
    public async Task RiderGetsNoDataSummaryForOwnTripWithoutMinorEvents()
    {
        Ctx c = Ctx.Create();
        TelemetrySummaryResponse response = await c.Service.GetForRiderAsync("rider", "trip", CancellationToken.None);
        response.SummaryStatus.Should().Be("NoData");
        response.TotalMinorEvents.Should().Be(0);
        response.EventsByType.Should().BeEmpty();
        response.FirstEventAtUtc.Should().BeNull();
        response.LastEventAtUtc.Should().BeNull();
        response.AverageConfidence.Should().BeNull();
        response.AverageScore.Should().BeNull();
        response.MaxScore.Should().BeNull();
        response.MinBatteryLevel.Should().BeNull();
        response.MaxSpeedKmh.Should().BeNull();
    }

    [Fact]
    public async Task RiderCannotGetForeignTripAndMonitorIsForbidden()
    {
        await Assert.ThrowsAsync<NotFoundAppException>(() => Ctx.Create().Service.GetForRiderAsync("other", "trip", CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenAppException>(() => Ctx.Create().Service.GetForRiderAsync("monitor", "trip", CancellationToken.None));
    }

    [Fact]
    public async Task ComputesAggregatesFromMinorEventsWithoutNullDefaults()
    {
        Ctx c = Ctx.Create(events: [Event(MinorEventType.HardBrake, MinorEventSeverity.Medium, MinorEventSource.MobileApp, MinorEventStatus.Recorded, Now.AddMinutes(-5), 80, .8, 20, 40, "Good"), Event(MinorEventType.LowBattery, MinorEventSeverity.Low, MinorEventSource.Smartwatch, MinorEventStatus.Reviewed, Now.AddMinutes(-1), null, .6, null, null, "Weak"), Event(MinorEventType.SmartwatchDisconnected, MinorEventSeverity.Info, MinorEventSource.OfflineIngestion, MinorEventStatus.Ignored, Now.AddMinutes(-3), 40, 1, 10, 60, "Good")]);

        TelemetrySummaryResponse response = await c.Service.RecomputeForRiderAsync("rider", "trip", CancellationToken.None);

        response.SummaryStatus.Should().Be("Computed");
        response.TotalMinorEvents.Should().Be(3);
        response.EventsByType["HardBrake"].Should().Be(1);
        response.EventsBySeverity["Medium"].Should().Be(1);
        response.EventsBySource["Smartwatch"].Should().Be(1);
        response.EventsByStatus["Reviewed"].Should().Be(1);
        response.HardBrakeCount.Should().Be(1);
        response.LowBatteryCount.Should().Be(1);
        response.SmartwatchDisconnectedCount.Should().Be(1);
        response.FirstEventAtUtc.Should().Be(Now.AddMinutes(-5));
        response.LastEventAtUtc.Should().Be(Now.AddMinutes(-1));
        response.AverageConfidence.Should().BeApproximately(.8, .0001);
        response.MaxScore.Should().Be(80);
        response.AverageScore.Should().Be(60);
        response.MinBatteryLevel.Should().Be(10);
        response.MaxSpeedKmh.Should().Be(60);
        response.GpsQualitySamples["Good"].Should().Be(2);
    }

    [Fact]
    public async Task RecomputeUpdatesSameDocumentAndPreservesCreatedAt()
    {
        Ctx c = Ctx.Create(events: [Event(MinorEventType.HardBrake, MinorEventSeverity.Low, MinorEventSource.MobileApp, MinorEventStatus.Recorded, Now, 10, .5, null, null, null)]);
        TelemetrySummaryResponse first = await c.Service.RecomputeForRiderAsync("rider", "trip", CancellationToken.None);
        c.Events.Items.Add(Event(MinorEventType.SharpTurn, MinorEventSeverity.Low, MinorEventSource.MobileApp, MinorEventStatus.Recorded, Now.AddMinutes(1), 20, .5, null, null, null));
        TelemetrySummaryResponse second = await c.Service.RecomputeForRiderAsync("rider", "trip", CancellationToken.None);

        second.Id.Should().Be(first.Id);
        second.CreatedAtUtc.Should().Be(first.CreatedAtUtc);
        second.TotalMinorEvents.Should().Be(2);
        c.Summaries.Items.Should().ContainSingle();
        c.Trips.Items.Single().Status.Should().Be(TripStatus.Active);
        c.Events.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task AdminListsAndGetsPersistedSummariesOnlyAndAuditFailureDoesNotBreak()
    {
        Ctx c = Ctx.Create(audit: new FailingAudit());
        TelemetrySummaryResponse created = await c.Service.RecomputeForRiderAsync("rider", "trip", CancellationToken.None);
        (await c.Service.ListForAdminAsync("admin", new TelemetrySummaryQuery(null, null, null, null, null, null, 1, 20), CancellationToken.None)).TotalCount.Should().Be(1);
        (await c.Service.GetForAdminAsync("admin", created.Id, CancellationToken.None)).Id.Should().Be(created.Id);
        await Assert.ThrowsAsync<ForbiddenAppException>(() => c.Service.ListForAdminAsync("rider", new TelemetrySummaryQuery(null, null, null, null, null, null, 1, 20), CancellationToken.None));
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);
    private static MinorEvent Event(MinorEventType type, MinorEventSeverity severity, MinorEventSource source, MinorEventStatus status, DateTimeOffset occurredAt, double? score, double confidence, int? battery, double? speed, string? gps) => new() { UserId = "rider", TripId = "trip", VehicleId = "vehicle", EventType = type, Severity = severity, Source = source, Status = status, Score = score, Confidence = confidence, BatteryLevel = battery, SpeedKmh = speed, GpsQuality = gps, OccurredAtUtc = occurredAt, CreatedAtUtc = occurredAt };
    private sealed class Ctx { public Trips Trips { get; private set; } = null!; public Events Events { get; private set; } = null!; public Summaries Summaries { get; private set; } = null!; public TelemetrySummaryService Service { get; private set; } = null!; public static Ctx Create(MinorEvent[]? events = null, IAuditLogService? audit = null) { var c = new Ctx(); c.Trips = new Trips(new Trip { Id = "trip", UserId = "rider", VehicleId = "vehicle", Status = TripStatus.Active, CreatedAtUtc = Now }); c.Events = new Events(events ?? []); c.Summaries = new Summaries(); c.Service = new TelemetrySummaryService(new Users(), c.Trips, c.Events, c.Summaries, new Clock(), audit ?? new Audit()); return c; } }
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => Now; }
    private sealed class Users : IUserRepository { public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult<User?>(id switch { "rider" => new User { Id = id, Role = UserRole.Rider, IsActive = true }, "other" => new User { Id = id, Role = UserRole.Rider, IsActive = true }, "monitor" => new User { Id = id, Role = UserRole.Monitor, IsActive = true }, "admin" => new User { Id = id, Role = UserRole.Admin, IsActive = true }, _ => null }); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult<User?>(null); public Task AddAsync(User user, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(User user, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Trips(params Trip[] items) : ITripRepository { public List<Trip> Items { get; } = items.ToList(); public Task<Trip?> GetActiveByUserIdAsync(string userId, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.UserId == userId && t.Status == TripStatus.Active)); public Task<Trip?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.Id == id)); public Task<IReadOnlyList<Trip>> ListByUserIdAsync(string userId, TripStatus? status, int pageNumber, int pageSize, CancellationToken ct) => Task.FromResult<IReadOnlyList<Trip>>([]); public Task<long> CountByUserIdAsync(string userId, TripStatus? status, CancellationToken ct) => Task.FromResult(0L); public Task AddAsync(Trip trip, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(Trip trip, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Events(params MinorEvent[] items) : IMinorEventRepository { public List<MinorEvent> Items { get; } = items.ToList(); public Task<MinorEvent?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(e => e.Id == id)); public Task<MinorEvent?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) => Task.FromResult<MinorEvent?>(null); public Task<(MinorEvent MinorEvent, bool IsDuplicate)> AddOrGetDuplicateAsync(MinorEvent minorEvent, CancellationToken ct) => throw new NotImplementedException(); public Task UpdateAsync(MinorEvent minorEvent, CancellationToken ct) => Task.CompletedTask; public Task<IReadOnlyList<MinorEvent>> ListByUserIdAsync(string userId, MinorEventQuery query, CancellationToken ct) => Task.FromResult<IReadOnlyList<MinorEvent>>([]); public Task<IReadOnlyList<MinorEvent>> ListByTripIdAsync(string userId, string tripId, CancellationToken ct) => Task.FromResult<IReadOnlyList<MinorEvent>>(Items.Where(e => e.UserId == userId && e.TripId == tripId).ToArray()); public Task<long> CountByUserIdAsync(string userId, MinorEventQuery query, CancellationToken ct) => Task.FromResult(0L); public Task<IReadOnlyList<MinorEvent>> ListAsync(MinorEventQuery query, CancellationToken ct) => Task.FromResult<IReadOnlyList<MinorEvent>>([]); public Task<long> CountAsync(MinorEventQuery query, CancellationToken ct) => Task.FromResult(0L); }
    private sealed class Summaries : ITelemetrySummaryRepository { public List<TripTelemetrySummary> Items { get; } = []; public Task<TripTelemetrySummary?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(s => s.Id == id)); public Task<TripTelemetrySummary?> GetByTripIdAsync(string tripId, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(s => s.TripId == tripId)); public Task<TripTelemetrySummary?> GetByUserIdAndTripIdAsync(string userId, string tripId, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(s => s.UserId == userId && s.TripId == tripId)); public Task<TripTelemetrySummary> UpsertAsync(TripTelemetrySummary summary, CancellationToken ct) { TripTelemetrySummary? existing = Items.FirstOrDefault(s => s.UserId == summary.UserId && s.TripId == summary.TripId); if (existing is not null) { summary.Id = existing.Id; summary.CreatedAtUtc = existing.CreatedAtUtc; Items.Remove(existing); } Items.Add(summary); return Task.FromResult(summary); } public Task<IReadOnlyList<TripTelemetrySummary>> ListByUserIdAsync(string userId, TelemetrySummaryQuery query, CancellationToken ct) => Task.FromResult<IReadOnlyList<TripTelemetrySummary>>(Items.Where(s => s.UserId == userId).ToArray()); public Task<long> CountByUserIdAsync(string userId, TelemetrySummaryQuery query, CancellationToken ct) => Task.FromResult((long)Items.Count(s => s.UserId == userId)); public Task<IReadOnlyList<TripTelemetrySummary>> ListAsync(TelemetrySummaryQuery query, CancellationToken ct) => Task.FromResult<IReadOnlyList<TripTelemetrySummary>>(Items); public Task<long> CountAsync(TelemetrySummaryQuery query, CancellationToken ct) => Task.FromResult((long)Items.Count); }
    private class Audit : IAuditLogService { public Task RecordAsync(string actorUserId, string actorRole, AuditAction action, AuditModule module, string entityType, string? entityId, AuditOutcome outcome, string? reason, string? requestPath, string? httpMethod, IReadOnlyDictionary<string, string>? metadata, CancellationToken cancellationToken) => Task.CompletedTask; public Task<GetAuditLogsResponse> ListAsync(string adminUserId, AuditLogQuery query, CancellationToken cancellationToken) => throw new NotImplementedException(); public Task<AuditLogResponse> GetAsync(string adminUserId, string id, CancellationToken cancellationToken) => throw new NotImplementedException(); }
    private sealed class FailingAudit : Audit { public new Task RecordAsync(string actorUserId, string actorRole, AuditAction action, AuditModule module, string entityType, string? entityId, AuditOutcome outcome, string? reason, string? requestPath, string? httpMethod, IReadOnlyDictionary<string, string>? metadata, CancellationToken cancellationToken) => throw new InvalidOperationException("audit failed"); }
}
