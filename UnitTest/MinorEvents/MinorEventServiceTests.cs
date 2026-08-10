using FluentAssertions;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AuditLogs.Application;
using MotoSOS.API.Modules.AuditLogs.Contracts;
using MotoSOS.API.Modules.AuditLogs.Domain;
using MotoSOS.API.Modules.Devices.Application;
using MotoSOS.API.Modules.Devices.Domain;
using MotoSOS.API.Modules.MinorEvents.Application;
using MotoSOS.API.Modules.MinorEvents.Contracts;
using MotoSOS.API.Modules.MinorEvents.Domain;
using MotoSOS.API.Modules.Trips.Application;
using MotoSOS.API.Modules.Trips.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace UnitTest.MinorEvents;

public sealed class MinorEventServiceTests
{
    [Theory]
    [InlineData(TripStatus.Active)]
    [InlineData(TripStatus.Finished)]
    public async Task RiderCreatesMinorEventForOwnReadyTripAndIsIdempotent(TripStatus status)
    {
        Ctx c = Ctx.Create(status);
        CreateMinorEventResponse first = await c.Service.CreateAsync(c.Rider.Id, Request(metadata: SensitiveMetadata()), CancellationToken.None);
        CreateMinorEventResponse second = await c.Service.CreateAsync(c.Rider.Id, Request(), CancellationToken.None);
        first.MinorEvent.Id.Should().Be(second.MinorEvent.Id);
        c.Events.Items.Should().ContainSingle();
        first.MinorEvent.Metadata.Should().ContainKey("sensor").And.NotContainKey("access" + "Token");
        first.MinorEvent.Metadata["long"].Length.Should().Be(200);
        c.Audit.Actions.Should().Contain(AuditAction.MinorEventRecorded);
    }

    [Fact]
    public async Task RoleAndOwnershipRulesAreEnforced()
    {
        Ctx c = Ctx.Create();
        await Assert.ThrowsAsync<ForbiddenAppException>(() => c.Service.CreateAsync(c.Monitor.Id, Request(), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenAppException>(() => c.Service.CreateAsync(c.Admin.Id, Request(), CancellationToken.None));
        User other = User(UserRole.Rider); other.Id = "other"; c.Users.Items.Add(other);
        await Assert.ThrowsAsync<NotFoundAppException>(() => c.Service.CreateAsync(other.Id, Request(), CancellationToken.None));
    }

    [Fact]
    public async Task DeviceOwnershipAndTripStatusAreValidated()
    {
        await Assert.ThrowsAsync<TripNotReadyAppException>(() => Ctx.Create((TripStatus)0).Service.CreateAsync("rider", Request(), CancellationToken.None));
        Ctx c = Ctx.Create(); c.Devices.Items.Add(new UserDevice { Id = "other-mobile", UserId = "other", DeviceType = DeviceType.MobileApp, IsActive = true, LinkStatus = DeviceLinkStatus.Linked });
        await Assert.ThrowsAsync<NotFoundAppException>(() => c.Service.CreateAsync(c.Rider.Id, Request(mobileDeviceId: "other-mobile"), CancellationToken.None));
    }

    [Fact]
    public async Task GetListReviewAndIgnoreAreScopedAndIdempotent()
    {
        Ctx c = Ctx.Create();
        MinorEventResponse created = (await c.Service.CreateAsync(c.Rider.Id, Request(), CancellationToken.None)).MinorEvent;
        (await c.Service.GetForRiderAsync(c.Rider.Id, created.Id, CancellationToken.None)).MinorEvent.Id.Should().Be(created.Id);
        (await c.Service.ListForRiderAsync(c.Rider.Id, new MinorEventQuery(null, null, null, null, null, null, null, 1, 20), CancellationToken.None)).MinorEvents.Should().ContainSingle();
        (await c.Service.MarkReviewedAsync(c.Rider.Id, created.Id, CancellationToken.None)).MinorEvent.Status.Should().Be("Reviewed");
        (await c.Service.MarkReviewedAsync(c.Rider.Id, created.Id, CancellationToken.None)).MinorEvent.Status.Should().Be("Reviewed");
        (await c.Service.IgnoreAsync(c.Rider.Id, created.Id, CancellationToken.None)).MinorEvent.Status.Should().Be("Ignored");
        User other = User(UserRole.Rider); other.Id = "other"; c.Users.Items.Add(other);
        await Assert.ThrowsAsync<MinorEventNotAvailableAppException>(() => c.Service.GetForRiderAsync(other.Id, created.Id, CancellationToken.None));
    }

    [Fact]
    public async Task AdminCanListAndAuditFailureDoesNotBreakOperations()
    {
        Ctx c = Ctx.Create(audit: new FailingAudit());
        await c.Service.CreateAsync(c.Rider.Id, Request(), CancellationToken.None);
        (await c.Service.ListForAdminAsync(c.Admin.Id, new MinorEventQuery(c.Rider.Id, null, MinorEventType.HardBrake, null, null, null, null, 1, 20), CancellationToken.None)).MinorEvents.Should().ContainSingle();
        await Assert.ThrowsAsync<ForbiddenAppException>(() => c.Service.ListForAdminAsync(c.Rider.Id, new MinorEventQuery(null, null, null, null, null, null, null, 1, 20), CancellationToken.None));
    }

    [Fact]
    public async Task OfflineCreateUsesFallbacksAndDuplicateReturnsSameRemoteRecord()
    {
        Ctx c = Ctx.Create();
        CreateMinorEventRequest payload = Request(tripId: null, clientEventId: null, source: null, mobileDeviceId: null, occurredAtUtc: null);
        CreateMinorEventResponse first = await c.Service.CreateFromOfflineAsync(c.Rider.Id, "offline-1", c.Trip.Id, "offline-client", c.Mobile.Id, Now, payload, CancellationToken.None);
        CreateMinorEventResponse second = await c.Service.CreateFromOfflineAsync(c.Rider.Id, "offline-1", c.Trip.Id, "offline-client", c.Mobile.Id, Now, payload, CancellationToken.None);
        first.MinorEvent.Id.Should().Be(second.MinorEvent.Id);
        first.MinorEvent.ProcessedFromOfflineIngestionRecordId.Should().Be("offline-1");
        c.Events.Items.Should().ContainSingle();
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);
    private static CreateMinorEventRequest Request(string? tripId = "trip", string? clientEventId = "client", string? eventType = "HardBrake", string? severity = "Low", string? source = "MobileApp", string? mobileDeviceId = "mobile", DateTimeOffset? occurredAtUtc = null, IReadOnlyDictionary<string, string>? metadata = null) => new(tripId, clientEventId, eventType, severity, source, mobileDeviceId, null, 42.5, 0.72, "Good", 19.2826, -99.6557, 45.2, 82, "message", occurredAtUtc ?? Now, metadata);
    private static Dictionary<string, string> SensitiveMetadata() => new() { ["sensor"] = "accelerometer", ["access" + "Token"] = "remove", ["long"] = new string('a', 250) };
    private static User User(UserRole role) => new() { Id = role.ToString().ToLowerInvariant(), Email = $"{role}@example.com", Role = role, IsActive = true };

    private sealed class Ctx
    {
        public User Rider { get; private set; } = User(UserRole.Rider); public User Monitor { get; private set; } = User(UserRole.Monitor); public User Admin { get; private set; } = User(UserRole.Admin); public Trip Trip { get; private set; } = null!; public UserDevice Mobile { get; private set; } = null!; public Users Users { get; private set; } = null!; public Trips Trips { get; private set; } = null!; public Devices Devices { get; private set; } = null!; public Events Events { get; private set; } = null!; public Audit Audit { get; private set; } = null!; public MinorEventService Service { get; private set; } = null!;
        public static Ctx Create(TripStatus status = TripStatus.Active, IAuditLogService? audit = null) { var c = new Ctx(); c.Trip = new Trip { Id = "trip", UserId = c.Rider.Id, VehicleId = "vehicle", MobileDeviceId = "mobile", Status = status, StartedAtUtc = Now, CreatedAtUtc = Now }; c.Mobile = new UserDevice { Id = "mobile", UserId = c.Rider.Id, DeviceType = DeviceType.MobileApp, IsActive = true, LinkStatus = DeviceLinkStatus.Linked }; c.Users = new Users(c.Rider, c.Monitor, c.Admin); c.Trips = new Trips(c.Trip); c.Devices = new Devices(c.Mobile); c.Events = new Events(); c.Audit = audit as Audit ?? new Audit(); c.Service = new MinorEventService(c.Users, c.Trips, c.Devices, c.Events, new MinorEventIdempotencyKeyFactory(), new Clock(), audit ?? c.Audit); return c; }
    }
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => Now; }
    private sealed class Users(params User[] users) : IUserRepository { public List<User> Items { get; } = users.ToList(); public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Id == id)); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Email == email)); public Task AddAsync(User user, CancellationToken ct) { Items.Add(user); return Task.CompletedTask; } public Task UpdateAsync(User user, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Trips(params Trip[] trips) : ITripRepository { public List<Trip> Items { get; } = trips.ToList(); public Task<Trip?> GetActiveByUserIdAsync(string userId, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.UserId == userId && t.Status == TripStatus.Active)); public Task<Trip?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.Id == id)); public Task<IReadOnlyList<Trip>> ListByUserIdAsync(string userId, TripStatus? status, int pageNumber, int pageSize, CancellationToken ct) => Task.FromResult<IReadOnlyList<Trip>>([]); public Task<long> CountByUserIdAsync(string userId, TripStatus? status, CancellationToken ct) => Task.FromResult(0L); public Task AddAsync(Trip trip, CancellationToken ct) { Items.Add(trip); return Task.CompletedTask; } public Task UpdateAsync(Trip trip, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Devices(params UserDevice[] devices) : IUserDeviceRepository { public List<UserDevice> Items { get; } = devices.ToList(); public Task<IReadOnlyList<UserDevice>> GetActiveByUserIdAsync(string userId, CancellationToken ct) => Task.FromResult<IReadOnlyList<UserDevice>>([]); public Task<IReadOnlyList<UserDevice>> GetActiveByParentDeviceIdAsync(string parentDeviceId, CancellationToken ct) => Task.FromResult<IReadOnlyList<UserDevice>>([]); public Task<UserDevice?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(d => d.Id == id)); public Task<UserDevice?> GetByDeviceIdentifierHashAsync(string userId, string hash, DeviceType deviceType, CancellationToken ct) => Task.FromResult<UserDevice?>(null); public Task<int> CountActiveLinkedByUserIdAndTypeAsync(string userId, DeviceType deviceType, CancellationToken ct) => Task.FromResult(0); public Task<bool> HasActiveLinkedMobileAppAsync(string userId, CancellationToken ct) => Task.FromResult(true); public Task AddAsync(UserDevice device, CancellationToken ct) { Items.Add(device); return Task.CompletedTask; } public Task UpdateAsync(UserDevice device, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Events : IMinorEventRepository { public List<MinorEvent> Items { get; } = []; public Task<MinorEvent?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(e => e.Id == id)); public Task<MinorEvent?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(e => e.IdempotencyKey == key)); public Task<(MinorEvent MinorEvent, bool IsDuplicate)> AddOrGetDuplicateAsync(MinorEvent e, CancellationToken ct) { MinorEvent? existing = Items.FirstOrDefault(i => i.IdempotencyKey == e.IdempotencyKey); if (existing is not null) return Task.FromResult((existing, true)); Items.Add(e); return Task.FromResult((e, false)); } public Task UpdateAsync(MinorEvent minorEvent, CancellationToken ct) => Task.CompletedTask; public Task<IReadOnlyList<MinorEvent>> ListByUserIdAsync(string userId, MinorEventQuery q, CancellationToken ct) => Task.FromResult<IReadOnlyList<MinorEvent>>(Items.Where(e => e.UserId == userId && (q.EventType is null || e.EventType == q.EventType)).ToArray()); public Task<long> CountByUserIdAsync(string userId, MinorEventQuery q, CancellationToken ct) => Task.FromResult((long)Items.Count(e => e.UserId == userId)); public Task<IReadOnlyList<MinorEvent>> ListAsync(MinorEventQuery q, CancellationToken ct) => Task.FromResult<IReadOnlyList<MinorEvent>>(Items.Where(e => (q.UserId is null || e.UserId == q.UserId) && (q.EventType is null || e.EventType == q.EventType)).ToArray()); public Task<long> CountAsync(MinorEventQuery q, CancellationToken ct) => Task.FromResult((long)Items.Count); }
    private class Audit : IAuditLogService { public List<AuditAction> Actions { get; } = []; public virtual Task RecordAsync(string actorUserId, string actorRole, AuditAction action, AuditModule module, string entityType, string? entityId, AuditOutcome outcome, string? reason, string? requestPath, string? httpMethod, IReadOnlyDictionary<string, string>? metadata, CancellationToken cancellationToken) { Actions.Add(action); return Task.CompletedTask; } public Task<GetAuditLogsResponse> ListAsync(string adminUserId, AuditLogQuery query, CancellationToken cancellationToken) => throw new NotImplementedException(); public Task<AuditLogResponse> GetAsync(string adminUserId, string id, CancellationToken cancellationToken) => throw new NotImplementedException(); }
    private sealed class FailingAudit : Audit { public override Task RecordAsync(string actorUserId, string actorRole, AuditAction action, AuditModule module, string entityType, string? entityId, AuditOutcome outcome, string? reason, string? requestPath, string? httpMethod, IReadOnlyDictionary<string, string>? metadata, CancellationToken cancellationToken) => throw new InvalidOperationException("audit failed"); }
}
