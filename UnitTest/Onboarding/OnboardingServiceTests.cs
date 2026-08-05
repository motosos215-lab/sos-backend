using FluentAssertions;
using MotoSOS.API.Modules.Onboarding.Application;
using MotoSOS.API.Modules.Onboarding.Contracts;
using MotoSOS.API.Modules.Profiles.Application;
using MotoSOS.API.Modules.Profiles.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;
using MotoSOS.API.Modules.Vehicles.Application;
using MotoSOS.API.Modules.Vehicles.Domain;

namespace UnitTest.Onboarding;

public sealed class OnboardingServiceTests
{
    [Fact]
    public async Task StatusWithoutProfileReturnsProfilePending()
    {
        var user = CreateRider();
        var service = CreateService(user, profile: null, vehicles: []);

        var response = await service.GetStatusAsync(user.Id, CancellationToken.None);

        response.CompletedSteps.Should().Be(1);
        response.ProgressPercentage.Should().Be(14);
        response.CurrentStep.Should().Be("Profile");
        AssertWizardSteps(response.Steps);
        response.Steps.Should().Contain(step => step.Key == "Profile" && step.Status == "Pending");
    }

    [Fact]
    public async Task StatusWithDraftProfileReturnsProfileInProgress()
    {
        var user = CreateRider();
        var profile = new DriverProfile { UserId = user.Id, CompletionStatus = ProfileCompletionStatus.Draft };
        var service = CreateService(user, profile, vehicles: []);

        var response = await service.GetStatusAsync(user.Id, CancellationToken.None);

        response.CompletedSteps.Should().Be(1);
        response.ProgressPercentage.Should().Be(14);
        response.CurrentStep.Should().Be("Profile");
        AssertWizardSteps(response.Steps);
        response.Steps.Should().Contain(step => step.Key == "Profile" && step.Status == "InProgress");
    }

    [Fact]
    public async Task StatusWithCompletedProfileReturnsVehicleCurrentStep()
    {
        var user = CreateRider();
        var profile = new DriverProfile { UserId = user.Id, CompletionStatus = ProfileCompletionStatus.Completed };
        var service = CreateService(user, profile, vehicles: []);

        var response = await service.GetStatusAsync(user.Id, CancellationToken.None);

        response.CompletedSteps.Should().Be(2);
        response.ProgressPercentage.Should().Be(29);
        response.CurrentStep.Should().Be("Vehicle");
        response.IsOperational.Should().BeFalse();
        AssertWizardSteps(response.Steps);
        response.Steps.Should().Contain(step => step.Key == "Profile" && step.Status == "Completed");
        response.Steps.Should().Contain(step => step.Key == "Vehicle" && step.Status == "Pending");
    }

    [Fact]
    public async Task StatusWithCompletedProfileAndDraftVehicleReturnsVehicleInProgress()
    {
        var user = CreateRider();
        var profile = new DriverProfile { UserId = user.Id, CompletionStatus = ProfileCompletionStatus.Completed };
        var vehicle = new DriverVehicle { UserId = user.Id, IsActive = true, CompletionStatus = VehicleCompletionStatus.Draft };
        var service = CreateService(user, profile, [vehicle]);

        var response = await service.GetStatusAsync(user.Id, CancellationToken.None);

        response.CompletedSteps.Should().Be(2);
        response.ProgressPercentage.Should().Be(29);
        response.CurrentStep.Should().Be("Vehicle");
        response.Steps.Should().Contain(step => step.Key == "Vehicle" && step.Status == "InProgress");
    }

    [Fact]
    public async Task StatusWithCompletedProfileAndCompletedVehicleReturnsEmergencyContactsCurrentStep()
    {
        var user = CreateRider();
        var profile = new DriverProfile { UserId = user.Id, CompletionStatus = ProfileCompletionStatus.Completed };
        var vehicle = new DriverVehicle { UserId = user.Id, IsActive = true, CompletionStatus = VehicleCompletionStatus.Completed };
        var service = CreateService(user, profile, [vehicle]);

        var response = await service.GetStatusAsync(user.Id, CancellationToken.None);

        response.CompletedSteps.Should().Be(3);
        response.ProgressPercentage.Should().Be(43);
        response.CurrentStep.Should().Be("EmergencyContacts");
        response.IsOperational.Should().BeFalse();
        response.Steps.Should().Contain(step => step.Key == "Profile" && step.Status == "Completed");
        response.Steps.Should().Contain(step => step.Key == "Vehicle" && step.Status == "Completed");
        response.Steps.Should().Contain(step => step.Key == "EmergencyContacts" && step.Status == "Pending");
    }

    [Fact]
    public async Task StatusWithDraftProfileAndCompletedVehicleDoesNotAdvanceVehicle()
    {
        var user = CreateRider();
        var profile = new DriverProfile { UserId = user.Id, CompletionStatus = ProfileCompletionStatus.Draft };
        var vehicle = new DriverVehicle { UserId = user.Id, IsActive = true, CompletionStatus = VehicleCompletionStatus.Completed };
        var service = CreateService(user, profile, [vehicle]);

        var response = await service.GetStatusAsync(user.Id, CancellationToken.None);

        response.CompletedSteps.Should().Be(1);
        response.ProgressPercentage.Should().Be(14);
        response.CurrentStep.Should().Be("Profile");
        response.Steps.Should().Contain(step => step.Key == "Profile" && step.Status == "InProgress");
        response.Steps.Should().Contain(step => step.Key == "Vehicle" && step.Status == "Pending");
    }

    private static void AssertWizardSteps(IReadOnlyList<OnboardingStepResponse> steps)
    {
        steps.Should().NotBeEmpty();
        steps.Should().HaveCount(7);
        steps.Select(step => step.Label).Should().Equal(
            "Cuenta",
            "Perfil",
            "Motocicleta / Motoneta",
            "Contactos de emergencia",
            "Vinculación de dispositivos",
            "Plan y licencia",
            "Confirmación");
    }

    private static User CreateRider() => new()
    {
        Email = "rider@example.com",
        FullName = "Moto Rider",
        Role = UserRole.Rider,
        IsActive = true
    };

    private static OnboardingService CreateService(User user, DriverProfile? profile, IReadOnlyList<DriverVehicle> vehicles) =>
        new(new InMemoryUserRepository(user), new InMemoryDriverProfileRepository(profile), new InMemoryDriverVehicleRepository(vehicles));

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

    private sealed class InMemoryDriverProfileRepository : IDriverProfileRepository
    {
        private readonly DriverProfile? _profile;

        public InMemoryDriverProfileRepository(DriverProfile? profile)
        {
            _profile = profile;
        }

        public Task<DriverProfile?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult(_profile?.UserId == userId ? _profile : null);

        public Task AddAsync(DriverProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UpdateAsync(DriverProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryDriverVehicleRepository : IDriverVehicleRepository
    {
        private readonly IReadOnlyList<DriverVehicle> _vehicles;

        public InMemoryDriverVehicleRepository(IReadOnlyList<DriverVehicle> vehicles)
        {
            _vehicles = vehicles;
        }

        public Task<IReadOnlyList<DriverVehicle>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DriverVehicle>>(_vehicles.Where(vehicle => vehicle.UserId == userId && vehicle.IsActive).ToArray());

        public Task<DriverVehicle?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult<DriverVehicle?>(null);

        public Task<int> CountActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(0);

        public Task AddAsync(DriverVehicle vehicle, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UpdateAsync(DriverVehicle vehicle, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
