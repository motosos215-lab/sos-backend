using FluentAssertions;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;
using MotoSOS.API.Modules.Vehicles.Application;
using MotoSOS.API.Modules.Vehicles.Contracts;
using MotoSOS.API.Modules.Vehicles.Domain;

namespace UnitTest.Vehicles;

public sealed class VehicleServiceTests
{
    [Fact]
    public async Task RiderCanCreateDraft()
    {
        var user = CreateUser(UserRole.Rider);
        var vehicles = new InMemoryDriverVehicleRepository();
        var service = CreateService(user, vehicles);

        CreateVehicleResponse response = await service.CreateMyVehicleAsync(user.Id, DraftRequest(), CancellationToken.None);

        response.Vehicle.CompletionStatus.Should().Be("Draft");
        response.Vehicle.IsPrimary.Should().BeTrue();
        vehicles.Vehicles.Should().ContainSingle();
    }

    [Fact]
    public async Task RiderCanCreateCompleted()
    {
        var user = CreateUser(UserRole.Rider);
        var vehicles = new InMemoryDriverVehicleRepository();
        var service = CreateService(user, vehicles);

        CreateVehicleResponse response = await service.CreateMyVehicleAsync(user.Id, ValidContinueRequest(), CancellationToken.None);

        response.Vehicle.CompletionStatus.Should().Be("Completed");
        response.Vehicle.CompletedAtUtc.Should().NotBeNull();
    }

    [Theory]
    [InlineData(UserRole.Monitor)]
    [InlineData(UserRole.Admin)]
    public async Task NonRiderCannotCreateVehicle(UserRole role)
    {
        var user = CreateUser(role);
        var service = CreateService(user, new InMemoryDriverVehicleRepository());

        Func<Task> act = () => service.CreateMyVehicleAsync(user.Id, DraftRequest(), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAppException>();
    }

    [Fact]
    public async Task BasicPlanDoesNotAllowSecondActiveVehicle()
    {
        var user = CreateUser(UserRole.Rider);
        var vehicles = new InMemoryDriverVehicleRepository(new DriverVehicle { UserId = user.Id, IsActive = true });
        var service = CreateService(user, vehicles);

        Func<Task> act = () => service.CreateMyVehicleAsync(user.Id, ValidContinueRequest(), CancellationToken.None);

        await act.Should().ThrowAsync<PlanLimitExceededAppException>();
    }

    [Fact]
    public async Task ListReturnsOnlyUserActiveVehicles()
    {
        var user = CreateUser(UserRole.Rider);
        var other = CreateUser(UserRole.Rider);
        var ownActive = new DriverVehicle { UserId = user.Id, IsActive = true };
        var ownInactive = new DriverVehicle { UserId = user.Id, IsActive = false };
        var otherActive = new DriverVehicle { UserId = other.Id, IsActive = true };
        var service = CreateService(user, new InMemoryDriverVehicleRepository(ownActive, ownInactive, otherActive));

        GetVehiclesResponse response = await service.GetMyVehiclesAsync(user.Id, CancellationToken.None);

        response.Vehicles.Should().ContainSingle(vehicle => vehicle.Id == ownActive.Id);
    }

    [Fact]
    public async Task CannotUpdateOtherUsersVehicle()
    {
        var user = CreateUser(UserRole.Rider);
        var other = CreateUser(UserRole.Rider);
        var otherVehicle = new DriverVehicle { UserId = other.Id, IsActive = true };
        var service = CreateService(user, new InMemoryDriverVehicleRepository(otherVehicle));

        Func<Task> act = () => service.UpdateMyVehicleAsync(user.Id, otherVehicle.Id, ValidUpdateRequest(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundAppException>();
    }

    [Fact]
    public async Task DeletePerformsLogicalDelete()
    {
        var user = CreateUser(UserRole.Rider);
        var vehicle = new DriverVehicle { UserId = user.Id, IsActive = true };
        var service = CreateService(user, new InMemoryDriverVehicleRepository(vehicle));

        await service.DeleteMyVehicleAsync(user.Id, vehicle.Id, CancellationToken.None);

        vehicle.IsActive.Should().BeFalse();
        vehicle.UpdatedAtUtc.Should().NotBeNull();
    }

    private static VehicleService CreateService(User user, InMemoryDriverVehicleRepository vehicles) =>
        new(new InMemoryUserRepository(user), vehicles, new TestClock());

    private static User CreateUser(UserRole role) => new()
    {
        Email = $"{role.ToString().ToLowerInvariant()}@example.com",
        FullName = "Moto User",
        Role = role,
        IsActive = true
    };

    private static CreateVehicleRequest DraftRequest() => new(null, "Yamaha", null, null, "Mi moto", null, null, null, null, null, "Draft");

    private static CreateVehicleRequest ValidContinueRequest() => new("Motorcycle", "Yamaha", "FZ 2.0", 2022, "Mi moto", "Personal", "Rojo", "ABC1234", "VIN123456789", "Daily", "Continue");

    private static UpdateVehicleRequest ValidUpdateRequest() => new("Scooter", "Italika", "WS", 2021, "Motoneta", "Work", "Negro", "XYZ1234", "VIN987654321", "Weekly", "Continue");

    private sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 8, 4, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class InMemoryUserRepository : IUserRepository
    {
        private readonly User _user;

        public InMemoryUserRepository(User user)
        {
            _user = user;
        }

        public Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult<User?>(_user.Id == id ? _user : null);

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) => Task.FromResult<User?>(null);

        public Task AddAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UpdateAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryDriverVehicleRepository : IDriverVehicleRepository
    {
        public List<DriverVehicle> Vehicles { get; }

        public InMemoryDriverVehicleRepository(params DriverVehicle[] vehicles)
        {
            Vehicles = vehicles.ToList();
        }

        public Task<IReadOnlyList<DriverVehicle>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DriverVehicle>>(Vehicles.Where(vehicle => vehicle.UserId == userId && vehicle.IsActive).ToArray());

        public Task<DriverVehicle?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(Vehicles.FirstOrDefault(vehicle => vehicle.Id == id));

        public Task<int> CountActiveByUserIdAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult(Vehicles.Count(vehicle => vehicle.UserId == userId && vehicle.IsActive));

        public Task AddAsync(DriverVehicle vehicle, CancellationToken cancellationToken)
        {
            Vehicles.Add(vehicle);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(DriverVehicle vehicle, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
