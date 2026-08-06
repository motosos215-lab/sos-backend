using System.Text.Json;
using FluentAssertions;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.Devices.Application;
using MotoSOS.API.Modules.Devices.Domain;
using MotoSOS.API.Modules.OfflineIngestion.Application;
using MotoSOS.API.Modules.OfflineIngestion.Contracts;
using MotoSOS.API.Modules.OfflineIngestion.Domain;
using MotoSOS.API.Modules.Trips.Application;
using MotoSOS.API.Modules.Trips.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace UnitTest.OfflineIngestion;

public sealed class OfflineIngestionServiceTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task RiderCanIngestValidBatchAndRecordsRemainPending()
    {
        User user = User(UserRole.Rider);
        UserDevice mobile = Mobile(user.Id);
        Trip trip = Trip(user.Id, mobile.Id, TripStatus.Active);
        var records = new InMemoryOfflineIngestionRepository();
        OfflineIngestionService service = CreateService(user, mobile, trip, records);

        OfflineIngestionBatchResponse response = await service.IngestBatchAsync(user.Id, Batch(mobile.Id, trip.Id), CancellationToken.None);

        response.Results.Should().ContainSingle(result => result.Status == "Accepted" && !result.IsDuplicate);
        records.Records.Should().ContainSingle(record => record.ProcessingStatus == OfflineIngestionProcessingStatus.PendingProcessing);
        response.Results[0].RemoteRecordId.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(UserRole.Monitor)]
    [InlineData(UserRole.Admin)]
    public async Task NonRidersReceiveForbidden(UserRole role)
    {
        User user = User(role);
        OfflineIngestionService service = CreateService(user);

        Func<Task> act = () => service.IngestBatchAsync(user.Id, Batch("mobile", "trip"), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAppException>();
    }

    [Fact]
    public async Task BatchRequiresOwnedLinkedMobileAndOwnedTrip()
    {
        User user = User(UserRole.Rider);
        User other = User(UserRole.Rider);
        UserDevice otherMobile = Mobile(other.Id);
        UserDevice revokedMobile = Mobile(user.Id, active: false);
        Trip otherTrip = Trip(other.Id, otherMobile.Id, TripStatus.Active);
        OfflineIngestionService service = CreateService(user, other, otherMobile, revokedMobile, otherTrip);

        await Assert.ThrowsAsync<NotFoundAppException>(() => service.IngestBatchAsync(user.Id, Batch(otherMobile.Id, otherTrip.Id), CancellationToken.None));
        await Assert.ThrowsAsync<TripNotReadyAppException>(() => service.IngestBatchAsync(user.Id, Batch(revokedMobile.Id, otherTrip.Id), CancellationToken.None));
    }

    [Fact]
    public async Task AcceptsFinishedTripsForLateSync()
    {
        User user = User(UserRole.Rider);
        UserDevice mobile = Mobile(user.Id);
        Trip trip = Trip(user.Id, mobile.Id, TripStatus.Finished);
        OfflineIngestionService service = CreateService(user, mobile, trip);

        OfflineIngestionBatchResponse response = await service.IngestBatchAsync(user.Id, Batch(mobile.Id, trip.Id), CancellationToken.None);

        response.Results.Should().ContainSingle(result => result.Status == "Accepted");
    }

    [Fact]
    public async Task DuplicateReturnsSameAckAndPartialRetryCanMixAcceptedAndDuplicate()
    {
        User user = User(UserRole.Rider);
        UserDevice mobile = Mobile(user.Id);
        Trip trip = Trip(user.Id, mobile.Id, TripStatus.Active);
        var records = new InMemoryOfflineIngestionRepository();
        OfflineIngestionService service = CreateService(user, mobile, trip, records);
        string eventId = Guid.NewGuid().ToString();

        OfflineIngestionBatchResponse first = await service.IngestBatchAsync(user.Id, Batch(mobile.Id, trip.Id, [Item(eventId)]), CancellationToken.None);
        OfflineIngestionBatchResponse retry = await service.IngestBatchAsync(user.Id, Batch(mobile.Id, trip.Id, [Item(eventId), Item(Guid.NewGuid().ToString())]), CancellationToken.None);

        retry.Results.Should().Contain(result => result.Status == "Duplicate" && result.AckId == first.Results[0].AckId && result.IsDuplicate);
        retry.Results.Should().Contain(result => result.Status == "Accepted" && !result.IsDuplicate);
        records.Records.Should().HaveCount(2);
    }

    [Fact]
    public async Task ResponseDoesNotReturnPayload()
    {
        User user = User(UserRole.Rider);
        UserDevice mobile = Mobile(user.Id);
        Trip trip = Trip(user.Id, mobile.Id, TripStatus.Active);
        OfflineIngestionService service = CreateService(user, mobile, trip);

        OfflineIngestionBatchResponse response = await service.IngestBatchAsync(user.Id, Batch(mobile.Id, trip.Id), CancellationToken.None);
        string json = JsonSerializer.Serialize(response, SerializerOptions);

        json.Should().NotContain("score");
        json.Should().NotContain("payload");
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 6, 7, 0, 0, TimeSpan.Zero);
    private static OfflineIngestionService CreateService(User user, params object[] deps) => new(
        new InMemoryUserRepository(new[] { user }.Concat(deps.OfType<User>()).ToArray()),
        new InMemoryDeviceRepository(deps.OfType<UserDevice>().ToArray()),
        new InMemoryTripRepository(deps.OfType<Trip>().ToArray()),
        deps.OfType<InMemoryOfflineIngestionRepository>().FirstOrDefault() ?? new InMemoryOfflineIngestionRepository(),
        new OfflineIngestionIdempotencyKeyFactory(),
        new PayloadHasher(),
        new TestClock());

    private static User User(UserRole role) => new() { Email = $"{Guid.NewGuid()}@example.com", FullName = "Rider", Role = role, IsActive = true };
    private static UserDevice Mobile(string userId, bool active = true) => new() { UserId = userId, DeviceType = DeviceType.MobileApp, DeviceName = "Phone", IsActive = active, LinkStatus = active ? DeviceLinkStatus.Linked : DeviceLinkStatus.Revoked };
    private static Trip Trip(string userId, string mobileId, TripStatus status) => new() { UserId = userId, MobileDeviceId = mobileId, VehicleId = "vehicle", Status = status, StartedAtUtc = Now, FinishedAtUtc = status == TripStatus.Finished ? Now.AddMinutes(10) : null, CreatedAtUtc = Now };
    private static OfflineIngestionBatchRequest Batch(string mobileId, string tripId, IReadOnlyList<OfflineIngestionItemRequest>? items = null) => new(Guid.NewGuid().ToString(), mobileId, tripId, 1, Now, "1.0.0", items ?? [Item(Guid.NewGuid().ToString())]);
    private static OfflineIngestionItemRequest Item(string eventId) => new(eventId, "minor-event", Now, 1, JsonDocument.Parse("{\"score\":35}").RootElement.Clone());

    private sealed class TestClock : IClock { public DateTimeOffset UtcNow => Now; }
    private sealed class InMemoryUserRepository : IUserRepository { private readonly List<User> _users; public InMemoryUserRepository(params User[] users) { _users = users.ToList(); } public Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(_users.FirstOrDefault(user => user.Id == id)); public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) => Task.FromResult<User?>(null); public Task AddAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask; public Task UpdateAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryDeviceRepository : IUserDeviceRepository { private readonly List<UserDevice> _devices; public InMemoryDeviceRepository(params UserDevice[] devices) { _devices = devices.ToList(); } public Task<IReadOnlyList<UserDevice>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<UserDevice>>(_devices.Where(device => device.UserId == userId && device.IsActive).ToArray()); public Task<IReadOnlyList<UserDevice>> GetActiveByParentDeviceIdAsync(string parentDeviceId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<UserDevice>>([]); public Task<UserDevice?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(_devices.FirstOrDefault(device => device.Id == id)); public Task<UserDevice?> GetByDeviceIdentifierHashAsync(string userId, string hash, DeviceType deviceType, CancellationToken cancellationToken) => Task.FromResult<UserDevice?>(null); public Task<int> CountActiveLinkedByUserIdAndTypeAsync(string userId, DeviceType deviceType, CancellationToken cancellationToken) => Task.FromResult(0); public Task<bool> HasActiveLinkedMobileAppAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(false); public Task AddAsync(UserDevice device, CancellationToken cancellationToken) => Task.CompletedTask; public Task UpdateAsync(UserDevice device, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryTripRepository : ITripRepository { private readonly List<Trip> _trips; public InMemoryTripRepository(params Trip[] trips) { _trips = trips.ToList(); } public Task<Trip?> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(_trips.FirstOrDefault(trip => trip.UserId == userId && trip.Status == TripStatus.Active)); public Task<Trip?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(_trips.FirstOrDefault(trip => trip.Id == id)); public Task<IReadOnlyList<Trip>> ListByUserIdAsync(string userId, TripStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Trip>>([]); public Task<long> CountByUserIdAsync(string userId, TripStatus? status, CancellationToken cancellationToken) => Task.FromResult(0L); public Task AddAsync(Trip trip, CancellationToken cancellationToken) => Task.CompletedTask; public Task UpdateAsync(Trip trip, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryOfflineIngestionRepository : IOfflineIngestionRepository { public List<OfflineIngestionRecord> Records { get; } = []; public Task<OfflineIngestionRecord?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) => Task.FromResult(Records.FirstOrDefault(record => record.IdempotencyKey == idempotencyKey)); public Task<(OfflineIngestionRecord Record, bool IsDuplicate)> AddOrGetDuplicateAsync(OfflineIngestionRecord record, CancellationToken cancellationToken) { OfflineIngestionRecord? existing = Records.FirstOrDefault(saved => saved.IdempotencyKey == record.IdempotencyKey); if (existing is not null) return Task.FromResult((existing, true)); Records.Add(record); return Task.FromResult((record, false)); } }
}
