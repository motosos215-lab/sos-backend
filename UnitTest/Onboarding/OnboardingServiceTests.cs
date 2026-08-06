using FluentAssertions;
using MotoSOS.API.Modules.Devices.Application;
using MotoSOS.API.Modules.Devices.Domain;
using MotoSOS.API.Modules.EmergencyContacts.Application;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
using MotoSOS.API.Modules.Onboarding.Application;
using MotoSOS.API.Modules.Onboarding.Contracts;
using MotoSOS.API.Modules.Onboarding.Domain;
using MotoSOS.API.Modules.Plans.Application;
using MotoSOS.API.Modules.Plans.Domain;
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
        var service = CreateService(user, profile: null, vehicles: [], contacts: []);

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
        var service = CreateService(user, profile, vehicles: [], contacts: []);

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
        var service = CreateService(user, profile, vehicles: [], contacts: []);

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
        var service = CreateService(user, profile, [vehicle], contacts: []);

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
        var service = CreateService(user, profile, [vehicle], contacts: []);

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
        var service = CreateService(user, profile, [vehicle], contacts: []);

        var response = await service.GetStatusAsync(user.Id, CancellationToken.None);

        response.CompletedSteps.Should().Be(1);
        response.ProgressPercentage.Should().Be(14);
        response.CurrentStep.Should().Be("Profile");
        response.Steps.Should().Contain(step => step.Key == "Profile" && step.Status == "InProgress");
        response.Steps.Should().Contain(step => step.Key == "Vehicle" && step.Status == "Pending");
    }

    [Theory]
    [InlineData(EmergencyContactInvitationStatus.Draft, "InProgress", 3, 43, "EmergencyContacts")]
    [InlineData(EmergencyContactInvitationStatus.Pending, "InProgress", 3, 43, "EmergencyContacts")]
    [InlineData(EmergencyContactInvitationStatus.Invited, "Completed", 4, 57, "Devices")]
    [InlineData(EmergencyContactInvitationStatus.Linked, "Completed", 4, 57, "Devices")]
    public async Task StatusWithCompletedVehicleAndContactReflectsEmergencyContactProgress(
        EmergencyContactInvitationStatus invitationStatus,
        string expectedStatus,
        int completedSteps,
        int progressPercentage,
        string currentStep)
    {
        var user = CreateRider();
        var profile = new DriverProfile { UserId = user.Id, CompletionStatus = ProfileCompletionStatus.Completed };
        var vehicle = new DriverVehicle { UserId = user.Id, IsActive = true, CompletionStatus = VehicleCompletionStatus.Completed };
        var contact = new EmergencyContact { UserId = user.Id, IsActive = true, InvitationStatus = invitationStatus };
        var service = CreateService(user, profile, [vehicle], [contact]);

        var response = await service.GetStatusAsync(user.Id, CancellationToken.None);

        response.CompletedSteps.Should().Be(completedSteps);
        response.ProgressPercentage.Should().Be(progressPercentage);
        response.CurrentStep.Should().Be(currentStep);
        response.Steps.Should().Contain(step => step.Key == "EmergencyContacts" && step.Status == expectedStatus);
    }

    [Fact]
    public async Task StatusWithoutCompletedVehicleDoesNotAdvanceEmergencyContacts()
    {
        var user = CreateRider();
        var profile = new DriverProfile { UserId = user.Id, CompletionStatus = ProfileCompletionStatus.Completed };
        var vehicle = new DriverVehicle { UserId = user.Id, IsActive = true, CompletionStatus = VehicleCompletionStatus.Draft };
        var contact = new EmergencyContact { UserId = user.Id, IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Invited };
        var service = CreateService(user, profile, [vehicle], [contact]);

        var response = await service.GetStatusAsync(user.Id, CancellationToken.None);

        response.CompletedSteps.Should().Be(2);
        response.ProgressPercentage.Should().Be(29);
        response.CurrentStep.Should().Be("Vehicle");
        response.Steps.Should().Contain(step => step.Key == "Vehicle" && step.Status == "InProgress");
        response.Steps.Should().Contain(step => step.Key == "EmergencyContacts" && step.Status == "Pending");
    }

    [Fact]
    public async Task StatusWithCompletedPreviousStepsAndNoMobileAppKeepsDevicesPending()
    {
        var user = CreateRider();
        var profile = new DriverProfile { UserId = user.Id, CompletionStatus = ProfileCompletionStatus.Completed };
        var vehicle = new DriverVehicle { UserId = user.Id, IsActive = true, CompletionStatus = VehicleCompletionStatus.Completed };
        var contact = new EmergencyContact { UserId = user.Id, IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Invited };
        var service = CreateService(user, profile, [vehicle], [contact]);

        var response = await service.GetStatusAsync(user.Id, CancellationToken.None);

        response.CompletedSteps.Should().Be(4);
        response.ProgressPercentage.Should().Be(57);
        response.CurrentStep.Should().Be("Devices");
        response.Steps.Should().Contain(step => step.Key == "Devices" && step.Status == "Pending");
    }

    [Fact]
    public async Task StatusWithCompletedPreviousStepsAndLinkedMobileAppAdvancesToPlan()
    {
        var user = CreateRider();
        var profile = new DriverProfile { UserId = user.Id, CompletionStatus = ProfileCompletionStatus.Completed };
        var vehicle = new DriverVehicle { UserId = user.Id, IsActive = true, CompletionStatus = VehicleCompletionStatus.Completed };
        var contact = new EmergencyContact { UserId = user.Id, IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Invited };
        var mobile = new UserDevice { UserId = user.Id, DeviceType = DeviceType.MobileApp, IsActive = true, LinkStatus = DeviceLinkStatus.Linked };
        var service = CreateService(user, profile, [vehicle], [contact], [mobile]);

        var response = await service.GetStatusAsync(user.Id, CancellationToken.None);

        response.CompletedSteps.Should().Be(5);
        response.ProgressPercentage.Should().Be(71);
        response.CurrentStep.Should().Be("Plan");
        response.Steps.Should().Contain(step => step.Key == "Devices" && step.Status == "Completed");
    }

    [Fact]
    public async Task StatusDoesNotAdvanceDevicesWhenEmergencyContactsAreNotCompleted()
    {
        var user = CreateRider();
        var profile = new DriverProfile { UserId = user.Id, CompletionStatus = ProfileCompletionStatus.Completed };
        var vehicle = new DriverVehicle { UserId = user.Id, IsActive = true, CompletionStatus = VehicleCompletionStatus.Completed };
        var contact = new EmergencyContact { UserId = user.Id, IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Pending };
        var mobile = new UserDevice { UserId = user.Id, DeviceType = DeviceType.MobileApp, IsActive = true, LinkStatus = DeviceLinkStatus.Linked };
        var service = CreateService(user, profile, [vehicle], [contact], [mobile]);

        var response = await service.GetStatusAsync(user.Id, CancellationToken.None);

        response.CompletedSteps.Should().Be(3);
        response.CurrentStep.Should().Be("EmergencyContacts");
        response.Steps.Should().Contain(step => step.Key == "Devices" && step.Status == "Pending");
    }

    [Fact]
    public async Task StatusDoesNotCompleteDevicesWithSmartwatchOnly()
    {
        var user = CreateRider();
        var profile = new DriverProfile { UserId = user.Id, CompletionStatus = ProfileCompletionStatus.Completed };
        var vehicle = new DriverVehicle { UserId = user.Id, IsActive = true, CompletionStatus = VehicleCompletionStatus.Completed };
        var contact = new EmergencyContact { UserId = user.Id, IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Invited };
        var smartwatch = new UserDevice { UserId = user.Id, DeviceType = DeviceType.Smartwatch, IsActive = true, LinkStatus = DeviceLinkStatus.Linked };
        var service = CreateService(user, profile, [vehicle], [contact], [smartwatch]);

        var response = await service.GetStatusAsync(user.Id, CancellationToken.None);

        response.CompletedSteps.Should().Be(4);
        response.CurrentStep.Should().Be("Devices");
        response.Steps.Should().Contain(step => step.Key == "Devices" && step.Status == "Pending");
    }

    [Fact]
    public async Task StatusWithCompletedPreviousStepsAndNoSubscriptionKeepsPlanPending()
    {
        var user = CreateRider();
        var profile = new DriverProfile { UserId = user.Id, CompletionStatus = ProfileCompletionStatus.Completed };
        var vehicle = new DriverVehicle { UserId = user.Id, IsActive = true, CompletionStatus = VehicleCompletionStatus.Completed };
        var contact = new EmergencyContact { UserId = user.Id, IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Invited };
        var mobile = new UserDevice { UserId = user.Id, DeviceType = DeviceType.MobileApp, IsActive = true, LinkStatus = DeviceLinkStatus.Linked };
        var service = CreateService(user, profile, [vehicle], [contact], [mobile]);

        var response = await service.GetStatusAsync(user.Id, CancellationToken.None);

        response.CompletedSteps.Should().Be(5);
        response.ProgressPercentage.Should().Be(71);
        response.CurrentStep.Should().Be("Plan");
        response.Steps.Should().Contain(step => step.Key == "Plan" && step.Status == "Pending");
    }

    [Fact]
    public async Task StatusWithCompletedPreviousStepsAndActiveSubscriptionAdvancesToConfirmation()
    {
        var user = CreateRider();
        var profile = new DriverProfile { UserId = user.Id, CompletionStatus = ProfileCompletionStatus.Completed };
        var vehicle = new DriverVehicle { UserId = user.Id, IsActive = true, CompletionStatus = VehicleCompletionStatus.Completed };
        var contact = new EmergencyContact { UserId = user.Id, IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Invited };
        var mobile = new UserDevice { UserId = user.Id, DeviceType = DeviceType.MobileApp, IsActive = true, LinkStatus = DeviceLinkStatus.Linked };
        var subscription = new UserSubscription { UserId = user.Id, Status = SubscriptionStatus.Active, PlanTier = PlanTier.Basic };
        var service = CreateService(user, profile, [vehicle], [contact], [mobile], [subscription]);

        var response = await service.GetStatusAsync(user.Id, CancellationToken.None);

        response.CompletedSteps.Should().Be(6);
        response.ProgressPercentage.Should().Be(86);
        response.CurrentStep.Should().Be("Confirmation");
        response.Steps.Should().Contain(step => step.Key == "Plan" && step.Status == "Completed");
        response.Steps.Should().Contain(step => step.Key == "Confirmation" && step.Status == "Pending");
    }

    [Fact]
    public async Task StatusDoesNotAdvancePlanWhenDevicesAreNotCompleted()
    {
        var user = CreateRider();
        var profile = new DriverProfile { UserId = user.Id, CompletionStatus = ProfileCompletionStatus.Completed };
        var vehicle = new DriverVehicle { UserId = user.Id, IsActive = true, CompletionStatus = VehicleCompletionStatus.Completed };
        var contact = new EmergencyContact { UserId = user.Id, IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Invited };
        var subscription = new UserSubscription { UserId = user.Id, Status = SubscriptionStatus.Active, PlanTier = PlanTier.Basic };
        var service = CreateService(user, profile, [vehicle], [contact], devices: [], subscriptions: [subscription]);

        var response = await service.GetStatusAsync(user.Id, CancellationToken.None);

        response.CompletedSteps.Should().Be(4);
        response.CurrentStep.Should().Be("Devices");
        response.Steps.Should().Contain(step => step.Key == "Plan" && step.Status == "Pending");
    }

    [Fact]
    public async Task StatusWithConfirmationCompletesWizardAndSetsOperational()
    {
        var user = CreateRider();
        var profile = new DriverProfile { UserId = user.Id, CompletionStatus = ProfileCompletionStatus.Completed };
        var vehicle = new DriverVehicle { UserId = user.Id, IsActive = true, CompletionStatus = VehicleCompletionStatus.Completed };
        var contact = new EmergencyContact { UserId = user.Id, IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Invited };
        var mobile = new UserDevice { UserId = user.Id, DeviceType = DeviceType.MobileApp, IsActive = true, LinkStatus = DeviceLinkStatus.Linked };
        var subscription = new UserSubscription { UserId = user.Id, Status = SubscriptionStatus.Active, PlanTier = PlanTier.Basic };
        var confirmation = new OnboardingConfirmation { UserId = user.Id, IsOperational = true };
        var service = CreateService(user, profile, [vehicle], [contact], [mobile], [subscription], [confirmation]);

        var response = await service.GetStatusAsync(user.Id, CancellationToken.None);

        response.CompletedSteps.Should().Be(7);
        response.ProgressPercentage.Should().Be(100);
        response.CurrentStep.Should().Be("Completed");
        response.IsOperational.Should().BeTrue();
        response.Steps.Should().Contain(step => step.Key == "Confirmation" && step.Status == "Completed");
    }

    [Fact]
    public async Task StatusDoesNotReportOperationalWhenPreviousStepIsMissingEvenWithConfirmation()
    {
        var user = CreateRider();
        var profile = new DriverProfile { UserId = user.Id, CompletionStatus = ProfileCompletionStatus.Completed };
        var vehicle = new DriverVehicle { UserId = user.Id, IsActive = true, CompletionStatus = VehicleCompletionStatus.Completed };
        var contact = new EmergencyContact { UserId = user.Id, IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Invited };
        var subscription = new UserSubscription { UserId = user.Id, Status = SubscriptionStatus.Active, PlanTier = PlanTier.Basic };
        var confirmation = new OnboardingConfirmation { UserId = user.Id, IsOperational = true };
        var service = CreateService(user, profile, [vehicle], [contact], devices: [], subscriptions: [subscription], confirmations: [confirmation]);

        var response = await service.GetStatusAsync(user.Id, CancellationToken.None);

        response.CompletedSteps.Should().Be(4);
        response.CurrentStep.Should().Be("Devices");
        response.IsOperational.Should().BeFalse();
        response.Steps.Should().Contain(step => step.Key == "Confirmation" && step.Status == "Pending");
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

    private static OnboardingService CreateService(User user, DriverProfile? profile, IReadOnlyList<DriverVehicle> vehicles, IReadOnlyList<EmergencyContact> contacts, IReadOnlyList<UserDevice>? devices = null, IReadOnlyList<UserSubscription>? subscriptions = null, IReadOnlyList<OnboardingConfirmation>? confirmations = null) =>
        new(new InMemoryUserRepository(user), new InMemoryDriverProfileRepository(profile), new InMemoryDriverVehicleRepository(vehicles), new InMemoryEmergencyContactRepository(contacts), new InMemoryUserDeviceRepository(devices ?? []), new InMemoryUserSubscriptionRepository(subscriptions ?? []), new InMemoryOnboardingConfirmationRepository(confirmations ?? []));

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

    private sealed class InMemoryEmergencyContactRepository : IEmergencyContactRepository
    {
        private readonly IReadOnlyList<EmergencyContact> _contacts;

        public InMemoryEmergencyContactRepository(IReadOnlyList<EmergencyContact> contacts)
        {
            _contacts = contacts;
        }

        public Task<IReadOnlyList<EmergencyContact>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EmergencyContact>>(_contacts.Where(contact => contact.UserId == userId && contact.IsActive).ToArray());

        public Task<EmergencyContact?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult<EmergencyContact?>(null);

        public Task<EmergencyContact?> GetByLinkingCodeAsync(string linkingCode, CancellationToken cancellationToken) => Task.FromResult<EmergencyContact?>(null);

        public Task<int> CountActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(0);

        public Task AddAsync(EmergencyContact contact, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UpdateAsync(EmergencyContact contact, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryUserDeviceRepository : IUserDeviceRepository
    {
        private readonly IReadOnlyList<UserDevice> _devices;

        public InMemoryUserDeviceRepository(IReadOnlyList<UserDevice> devices)
        {
            _devices = devices;
        }

        public Task<IReadOnlyList<UserDevice>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<UserDevice>>(_devices.Where(device => device.UserId == userId && device.IsActive).ToArray());

        public Task<IReadOnlyList<UserDevice>> GetActiveByParentDeviceIdAsync(string parentDeviceId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<UserDevice>>([]);
        public Task<UserDevice?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult<UserDevice?>(null);
        public Task<UserDevice?> GetByDeviceIdentifierHashAsync(string userId, string hash, DeviceType deviceType, CancellationToken cancellationToken) => Task.FromResult<UserDevice?>(null);
        public Task<int> CountActiveLinkedByUserIdAndTypeAsync(string userId, DeviceType deviceType, CancellationToken cancellationToken) => Task.FromResult(0);

        public Task<bool> HasActiveLinkedMobileAppAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult(_devices.Any(device => device.UserId == userId && device.DeviceType == DeviceType.MobileApp && device.IsActive && device.LinkStatus == DeviceLinkStatus.Linked));

        public Task AddAsync(UserDevice device, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task UpdateAsync(UserDevice device, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryUserSubscriptionRepository : IUserSubscriptionRepository
    {
        private readonly IReadOnlyList<UserSubscription> _subscriptions;

        public InMemoryUserSubscriptionRepository(IReadOnlyList<UserSubscription> subscriptions)
        {
            _subscriptions = subscriptions;
        }

        public Task<UserSubscription?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult(_subscriptions.FirstOrDefault(subscription => subscription.UserId == userId));

        public Task<bool> HasActiveSubscriptionAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult(_subscriptions.Any(subscription => subscription.UserId == userId && subscription.Status == SubscriptionStatus.Active));

        public Task AddAsync(UserSubscription subscription, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task UpdateAsync(UserSubscription subscription, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryOnboardingConfirmationRepository : IOnboardingConfirmationRepository
    {
        private readonly IReadOnlyList<OnboardingConfirmation> _confirmations;

        public InMemoryOnboardingConfirmationRepository(IReadOnlyList<OnboardingConfirmation> confirmations)
        {
            _confirmations = confirmations;
        }

        public Task<OnboardingConfirmation?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult(_confirmations.FirstOrDefault(confirmation => confirmation.UserId == userId));

        public Task AddAsync(OnboardingConfirmation confirmation, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task UpdateAsync(OnboardingConfirmation confirmation, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
