using FluentAssertions;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.Devices.Application;
using MotoSOS.API.Modules.Devices.Domain;
using MotoSOS.API.Modules.Onboarding.Application;
using MotoSOS.API.Modules.Onboarding.Contracts;
using MotoSOS.API.Modules.Trips.Application;
using MotoSOS.API.Modules.Trips.Contracts;
using MotoSOS.API.Modules.Trips.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;
using MotoSOS.API.Modules.Vehicles.Application;
using MotoSOS.API.Modules.Vehicles.Domain;

namespace UnitTest.Trips;

public sealed class TripServiceTests
{
    [Fact]
    public async Task RiderCanStartTripWhenReady()
    {
        User user = User(UserRole.Rider);
        DriverVehicle vehicle = Vehicle(user.Id);
        UserDevice mobile = Mobile(user.Id);
        var trips = new InMemoryTripRepository();
        TripService service = CreateService(user, vehicle, mobile, trips);

        StartTripResponse response = await service.StartAsync(user.Id, StartRequest(vehicle.Id, mobile.Id), CancellationToken.None);

        response.Trip.Status.Should().Be("Active");
        response.Trip.VehicleId.Should().Be(vehicle.Id);
        trips.Trips.Should().ContainSingle();
    }

    [Fact]
    public async Task StartRequiresCompletedOnboarding()
    {
        User user = User(UserRole.Rider);
        DriverVehicle vehicle = Vehicle(user.Id);
        UserDevice mobile = Mobile(user.Id);
        TripService service = CreateService(user, vehicle, mobile, false);

        Func<Task> act = () => service.StartAsync(user.Id, StartRequest(vehicle.Id, mobile.Id), CancellationToken.None);

        await act.Should().ThrowAsync<OnboardingNotReadyAppException>();
    }

    [Fact]
    public async Task StartRequiresOwnCompletedVehicleAndOwnLinkedMobile()
    {
        User user = User(UserRole.Rider);
        User other = User(UserRole.Rider);
        DriverVehicle ownDraft = Vehicle(user.Id, completed: false);
        DriverVehicle otherVehicle = Vehicle(other.Id);
        UserDevice otherMobile = Mobile(other.Id);
        TripService service = CreateService(user, ownDraft, otherVehicle, otherMobile);

        await Assert.ThrowsAsync<TripNotReadyAppException>(() => service.StartAsync(user.Id, StartRequest(ownDraft.Id, otherMobile.Id), CancellationToken.None));
        await Assert.ThrowsAsync<TripNotReadyAppException>(() => service.StartAsync(user.Id, StartRequest(otherVehicle.Id, otherMobile.Id), CancellationToken.None));
    }

    [Fact]
    public async Task StartRequiresActiveLinkedMobileAndValidSmartwatchParent()
    {
        User user = User(UserRole.Rider);
        DriverVehicle vehicle = Vehicle(user.Id);
        UserDevice mobile = Mobile(user.Id, active: false);
        UserDevice linkedMobile = Mobile(user.Id);
        UserDevice watch = Watch(user.Id, "different");
        TripService service = CreateService(user, vehicle, mobile, linkedMobile, watch);

        await Assert.ThrowsAsync<TripNotReadyAppException>(() => service.StartAsync(user.Id, StartRequest(vehicle.Id, mobile.Id), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundAppException>(() => service.StartAsync(user.Id, StartRequest(vehicle.Id, linkedMobile.Id, watch.Id), CancellationToken.None));
    }

    [Fact]
    public async Task OnlyOneActiveTripIsAllowedAndSameStartIsIdempotent()
    {
        User user = User(UserRole.Rider);
        DriverVehicle vehicle = Vehicle(user.Id);
        DriverVehicle otherVehicle = Vehicle(user.Id);
        UserDevice mobile = Mobile(user.Id);
        var active = new Trip { UserId = user.Id, VehicleId = vehicle.Id, MobileDeviceId = mobile.Id, Status = TripStatus.Active, StartedAtUtc = Now, CreatedAtUtc = Now };
        TripService service = CreateService(user, vehicle, otherVehicle, mobile, new InMemoryTripRepository(active));

        StartTripResponse same = await service.StartAsync(user.Id, StartRequest(vehicle.Id, mobile.Id), CancellationToken.None);
        Func<Task> different = () => service.StartAsync(user.Id, StartRequest(otherVehicle.Id, mobile.Id), CancellationToken.None);

        same.Trip.Id.Should().Be(active.Id);
        await different.Should().ThrowAsync<ActiveTripExistsAppException>();
    }

    [Fact]
    public async Task ActiveGetListAndFinishRespectOwnershipAndIdempotency()
    {
        User user = User(UserRole.Rider);
        User other = User(UserRole.Rider);
        var active = new Trip { UserId = user.Id, VehicleId = "vehicle", MobileDeviceId = "mobile", Status = TripStatus.Active, StartedAtUtc = Now, CreatedAtUtc = Now };
        var otherTrip = new Trip { UserId = other.Id, VehicleId = "vehicle", MobileDeviceId = "mobile", Status = TripStatus.Active, StartedAtUtc = Now, CreatedAtUtc = Now };
        var trips = new InMemoryTripRepository(active, otherTrip);
        TripService service = CreateService(user, trips);

        (await service.GetActiveAsync(user.Id, CancellationToken.None)).Trip!.Id.Should().Be(active.Id);
        (await service.ListAsync(user.Id, null, null, null, CancellationToken.None)).Trips.Should().ContainSingle(trip => trip.Id == active.Id);
        await Assert.ThrowsAsync<NotFoundAppException>(() => service.FinishAsync(user.Id, otherTrip.Id, new FinishTripRequest(null, null, null, null), CancellationToken.None));

        FinishTripResponse first = await service.FinishAsync(user.Id, active.Id, new FinishTripRequest(Now.AddMinutes(30), null, 75, "done"), CancellationToken.None);
        FinishTripResponse second = await service.FinishAsync(user.Id, active.Id, new FinishTripRequest(null, null, null, null), CancellationToken.None);

        first.Trip.Status.Should().Be("Finished");
        second.Trip.FinishedAtUtc.Should().Be(first.Trip.FinishedAtUtc);
    }

    [Theory]
    [InlineData(UserRole.Monitor)]
    [InlineData(UserRole.Admin)]
    public async Task NonRidersReceiveForbidden(UserRole role)
    {
        User user = User(role);
        TripService service = CreateService(user);

        Func<Task> act = () => service.GetActiveAsync(user.Id, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAppException>();
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 6, 6, 0, 0, TimeSpan.Zero);

    private static TripService CreateService(User user, params object[] dependencies)
    {
        var users = new InMemoryUserRepository(new[] { user }.Concat(dependencies.OfType<User>()).ToArray());
        var vehicles = new InMemoryVehicleRepository(dependencies.OfType<DriverVehicle>().ToArray());
        var devices = new InMemoryDeviceRepository(dependencies.OfType<UserDevice>().ToArray());
        var trips = dependencies.OfType<InMemoryTripRepository>().FirstOrDefault() ?? new InMemoryTripRepository(dependencies.OfType<Trip>().ToArray());
        bool onboardingReady = dependencies.OfType<bool>().FirstOrDefault(true);
        return new TripService(users, new StubOnboardingService(onboardingReady), vehicles, devices, trips, new TestClock());
    }

    private static User User(UserRole role) => new() { Email = $"{Guid.NewGuid()}@example.com", FullName = "Rider", Role = role, IsActive = true };
    private static DriverVehicle Vehicle(string userId, bool completed = true) => new() { UserId = userId, IsActive = true, CompletionStatus = completed ? VehicleCompletionStatus.Completed : VehicleCompletionStatus.Draft };
    private static UserDevice Mobile(string userId, bool active = true) => new() { UserId = userId, DeviceType = DeviceType.MobileApp, DeviceName = "Phone", IsActive = active, LinkStatus = DeviceLinkStatus.Linked };
    private static UserDevice Watch(string userId, string parentId) => new() { UserId = userId, DeviceType = DeviceType.Smartwatch, DeviceName = "Watch", IsActive = true, LinkStatus = DeviceLinkStatus.Linked, ParentDeviceId = parentId };
    private static StartTripRequest StartRequest(string vehicleId, string mobileId, string? watchId = null) => new(vehicleId, mobileId, watchId, Now, new TripLocationRequest(19.2826, -99.6557, 12.5, "gps", Now), 87, "1.0.0");

    private sealed class TestClock : IClock { public DateTimeOffset UtcNow => Now; }
    private sealed class StubOnboardingService : IOnboardingService
    {
        private readonly bool _ready;
        public StubOnboardingService(bool ready) { _ready = ready; }
        public Task<OnboardingStatusResponse> GetStatusAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(_ready
            ? new OnboardingStatusResponse(7, 7, 100, "Completed", true, [])
            : new OnboardingStatusResponse(7, 6, 86, "Confirmation", false, []));
    }
    private sealed class InMemoryUserRepository : IUserRepository { private readonly List<User> _users; public InMemoryUserRepository(params User[] users) { _users = users.ToList(); } public Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(_users.FirstOrDefault(user => user.Id == id)); public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) => Task.FromResult<User?>(null); public Task AddAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask; public Task UpdateAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryVehicleRepository : IDriverVehicleRepository { private readonly List<DriverVehicle> _vehicles; public InMemoryVehicleRepository(params DriverVehicle[] vehicles) { _vehicles = vehicles.ToList(); } public Task<IReadOnlyList<DriverVehicle>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DriverVehicle>>(_vehicles.Where(vehicle => vehicle.UserId == userId && vehicle.IsActive).ToArray()); public Task<DriverVehicle?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(_vehicles.FirstOrDefault(vehicle => vehicle.Id == id)); public Task<int> CountActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(_vehicles.Count(vehicle => vehicle.UserId == userId && vehicle.IsActive)); public Task AddAsync(DriverVehicle vehicle, CancellationToken cancellationToken) { _vehicles.Add(vehicle); return Task.CompletedTask; } public Task UpdateAsync(DriverVehicle vehicle, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryDeviceRepository : IUserDeviceRepository { private readonly List<UserDevice> _devices; public InMemoryDeviceRepository(params UserDevice[] devices) { _devices = devices.ToList(); } public Task<IReadOnlyList<UserDevice>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<UserDevice>>(_devices.Where(device => device.UserId == userId && device.IsActive).ToArray()); public Task<IReadOnlyList<UserDevice>> GetActiveByParentDeviceIdAsync(string parentDeviceId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<UserDevice>>(_devices.Where(device => device.ParentDeviceId == parentDeviceId && device.IsActive).ToArray()); public Task<UserDevice?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(_devices.FirstOrDefault(device => device.Id == id)); public Task<UserDevice?> GetByDeviceIdentifierHashAsync(string userId, string hash, DeviceType deviceType, CancellationToken cancellationToken) => Task.FromResult<UserDevice?>(null); public Task<int> CountActiveLinkedByUserIdAndTypeAsync(string userId, DeviceType deviceType, CancellationToken cancellationToken) => Task.FromResult(0); public Task<bool> HasActiveLinkedMobileAppAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(_devices.Any(device => device.UserId == userId && device.DeviceType == DeviceType.MobileApp && device.IsActive && device.LinkStatus == DeviceLinkStatus.Linked)); public Task AddAsync(UserDevice device, CancellationToken cancellationToken) { _devices.Add(device); return Task.CompletedTask; } public Task UpdateAsync(UserDevice device, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryTripRepository : ITripRepository { public List<Trip> Trips { get; } public InMemoryTripRepository(params Trip[] trips) { Trips = trips.ToList(); } public Task<Trip?> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Trips.FirstOrDefault(trip => trip.UserId == userId && trip.Status == TripStatus.Active)); public Task<Trip?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(Trips.FirstOrDefault(trip => trip.Id == id)); public Task<IReadOnlyList<Trip>> ListByUserIdAsync(string userId, TripStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Trip>>(Trips.Where(trip => trip.UserId == userId && (!status.HasValue || trip.Status == status.Value)).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToArray()); public Task<long> CountByUserIdAsync(string userId, TripStatus? status, CancellationToken cancellationToken) => Task.FromResult((long)Trips.Count(trip => trip.UserId == userId && (!status.HasValue || trip.Status == status.Value))); public Task AddAsync(Trip trip, CancellationToken cancellationToken) { Trips.Add(trip); return Task.CompletedTask; } public Task UpdateAsync(Trip trip, CancellationToken cancellationToken) => Task.CompletedTask; }
}
