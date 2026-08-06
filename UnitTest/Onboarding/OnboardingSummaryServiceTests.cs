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

public sealed class OnboardingSummaryServiceTests
{
    [Fact]
    public async Task SummaryReturnsCanConfirmFalseWhenStepsAreMissing()
    {
        var user = CreateUser();
        var service = CreateService(user, Status(5, 71, "Plan", false, planStatus: "Pending"));

        OnboardingSummaryResponse response = await service.GetSummaryAsync(user.Id, CancellationToken.None);

        response.Summary.CanConfirm.Should().BeFalse();
        response.Summary.IsOperational.Should().BeFalse();
    }

    [Fact]
    public async Task SummaryReturnsFullSafeDataWhenReady()
    {
        var user = CreateUser();
        var service = CreateService(user, Status(6, 86, "Confirmation", false));

        OnboardingSummaryResponse response = await service.GetSummaryAsync(user.Id, CancellationToken.None);

        response.Summary.CanConfirm.Should().BeTrue();
        response.Summary.User.Email.Should().Be(user.Email);
        response.Summary.Profile!.PrimaryCity.Should().Be("Toluca");
        response.Summary.Vehicle!.Alias.Should().Be("Mi moto");
        response.Summary.EmergencyContact!.InvitationStatus.Should().Be("Invited");
        response.Summary.MobileDevice!.DeviceType.Should().Be("MobileApp");
        response.Summary.Smartwatch!.DeviceType.Should().Be("Smartwatch");
        response.Summary.Subscription!.PlanTier.Should().Be("Basic");
    }

    [Fact]
    public async Task SummaryReturnsConfirmedOperationalWhenStatusIsOperational()
    {
        var user = CreateUser();
        var confirmation = new OnboardingConfirmation { UserId = user.Id, IsOperational = true };
        var service = CreateService(user, Status(7, 100, "Completed", true, confirmationStatus: "Completed"), confirmation);

        OnboardingSummaryResponse response = await service.GetSummaryAsync(user.Id, CancellationToken.None);

        response.Summary.IsConfirmed.Should().BeTrue();
        response.Summary.IsOperational.Should().BeTrue();
        response.Summary.CurrentStep.Should().Be("Completed");
    }

    private static User CreateUser() => new() { Email = "rider@example.com", FullName = "Moto Rider", PhoneNumber = "+52 5512345678", Role = UserRole.Rider, IsActive = true };
    private static OnboardingStatusResponse Status(int completedSteps, int progress, string currentStep, bool isOperational, string planStatus = "Completed", string confirmationStatus = "Pending") => new(7, completedSteps, progress, currentStep, isOperational,
    [
        Step("Account", "Completed"), Step("Profile", "Completed"), Step("Vehicle", "Completed"), Step("EmergencyContacts", "Completed"), Step("Devices", "Completed"), Step("Plan", planStatus), Step("Confirmation", confirmationStatus)
    ]);

    private static OnboardingStepResponse Step(string key, string status) => new(key, 1, key, status);

    private static OnboardingSummaryService CreateService(User user, OnboardingStatusResponse status, OnboardingConfirmation? confirmation = null) => new(
        new InMemoryUserRepository(user),
        new InMemoryProfileRepository(new DriverProfile { UserId = user.Id, PrimaryCity = "Toluca", AddressOrZone = "Zona Centro", CompletionStatus = ProfileCompletionStatus.Completed }),
        new InMemoryVehicleRepository(new DriverVehicle { UserId = user.Id, IsActive = true, IsPrimary = true, CompletionStatus = VehicleCompletionStatus.Completed, VehicleType = VehicleType.Motorcycle, Brand = "Yamaha", Model = "FZ", Year = 2024, Alias = "Mi moto" }),
        new InMemoryContactRepository(new EmergencyContact { UserId = user.Id, IsActive = true, IsPrimary = true, FullName = "Contacto Uno", PhoneNumber = "+52 5511111111", Relationship = "Hermano", InvitationStatus = EmergencyContactInvitationStatus.Invited }),
        new InMemoryDeviceRepository(
            new UserDevice { UserId = user.Id, IsActive = true, DeviceType = DeviceType.MobileApp, DeviceName = "Motorola Edge", Platform = DevicePlatform.Android, LinkStatus = DeviceLinkStatus.Linked, ConnectionStatus = DeviceConnectionStatus.Online, BatteryLevel = 87 },
            new UserDevice { UserId = user.Id, IsActive = true, DeviceType = DeviceType.Smartwatch, DeviceName = "Galaxy Watch", Platform = DevicePlatform.WearOS, LinkStatus = DeviceLinkStatus.Linked, ConnectionStatus = DeviceConnectionStatus.Online, BatteryLevel = 80 }),
        new InMemorySubscriptionRepository(new UserSubscription { UserId = user.Id, PlanTier = PlanTier.Basic, Status = SubscriptionStatus.Active, Source = SubscriptionSource.WebBasic }),
        new InMemoryConfirmationRepository(confirmation),
        new FakeOnboardingService(status));

    private sealed class FakeOnboardingService : IOnboardingService { private readonly OnboardingStatusResponse _status; public FakeOnboardingService(OnboardingStatusResponse status) { _status = status; } public Task<OnboardingStatusResponse> GetStatusAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(_status); }
    private sealed class InMemoryUserRepository : IUserRepository { private readonly User _user; public InMemoryUserRepository(User user) { _user = user; } public Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult<User?>(_user.Id == id ? _user : null); public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) => Task.FromResult<User?>(null); public Task AddAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask; public Task UpdateAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryProfileRepository : IDriverProfileRepository { private readonly DriverProfile _profile; public InMemoryProfileRepository(DriverProfile profile) { _profile = profile; } public Task<DriverProfile?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<DriverProfile?>(_profile.UserId == userId ? _profile : null); public Task AddAsync(DriverProfile profile, CancellationToken cancellationToken) => Task.CompletedTask; public Task UpdateAsync(DriverProfile profile, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryVehicleRepository : IDriverVehicleRepository { private readonly DriverVehicle _vehicle; public InMemoryVehicleRepository(DriverVehicle vehicle) { _vehicle = vehicle; } public Task<IReadOnlyList<DriverVehicle>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DriverVehicle>>(_vehicle.UserId == userId && _vehicle.IsActive ? [_vehicle] : []); public Task<DriverVehicle?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult<DriverVehicle?>(null); public Task<int> CountActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(0); public Task AddAsync(DriverVehicle vehicle, CancellationToken cancellationToken) => Task.CompletedTask; public Task UpdateAsync(DriverVehicle vehicle, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryContactRepository : IEmergencyContactRepository { private readonly EmergencyContact _contact; public InMemoryContactRepository(EmergencyContact contact) { _contact = contact; } public Task<IReadOnlyList<EmergencyContact>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<EmergencyContact>>(_contact.UserId == userId && _contact.IsActive ? [_contact] : []); public Task<EmergencyContact?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult<EmergencyContact?>(null); public Task<EmergencyContact?> GetByLinkingCodeAsync(string linkingCode, CancellationToken cancellationToken) => Task.FromResult<EmergencyContact?>(null); public Task<int> CountActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(0); public Task AddAsync(EmergencyContact contact, CancellationToken cancellationToken) => Task.CompletedTask; public Task UpdateAsync(EmergencyContact contact, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryDeviceRepository : IUserDeviceRepository { private readonly IReadOnlyList<UserDevice> _devices; public InMemoryDeviceRepository(params UserDevice[] devices) { _devices = devices; } public Task<IReadOnlyList<UserDevice>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<UserDevice>>(_devices.Where(device => device.UserId == userId && device.IsActive).ToArray()); public Task<IReadOnlyList<UserDevice>> GetActiveByParentDeviceIdAsync(string parentDeviceId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<UserDevice>>([]); public Task<UserDevice?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult<UserDevice?>(null); public Task<UserDevice?> GetByDeviceIdentifierHashAsync(string userId, string hash, DeviceType deviceType, CancellationToken cancellationToken) => Task.FromResult<UserDevice?>(null); public Task<int> CountActiveLinkedByUserIdAndTypeAsync(string userId, DeviceType deviceType, CancellationToken cancellationToken) => Task.FromResult(0); public Task<bool> HasActiveLinkedMobileAppAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(_devices.Any(device => device.UserId == userId && device.DeviceType == DeviceType.MobileApp && device.IsActive && device.LinkStatus == DeviceLinkStatus.Linked)); public Task AddAsync(UserDevice device, CancellationToken cancellationToken) => Task.CompletedTask; public Task UpdateAsync(UserDevice device, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemorySubscriptionRepository : IUserSubscriptionRepository { private readonly UserSubscription _subscription; public InMemorySubscriptionRepository(UserSubscription subscription) { _subscription = subscription; } public Task<UserSubscription?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<UserSubscription?>(_subscription.UserId == userId ? _subscription : null); public Task<bool> HasActiveSubscriptionAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(_subscription.UserId == userId && _subscription.Status == SubscriptionStatus.Active); public Task AddAsync(UserSubscription subscription, CancellationToken cancellationToken) => Task.CompletedTask; public Task UpdateAsync(UserSubscription subscription, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryConfirmationRepository : IOnboardingConfirmationRepository { private readonly OnboardingConfirmation? _confirmation; public InMemoryConfirmationRepository(OnboardingConfirmation? confirmation) { _confirmation = confirmation; } public Task<OnboardingConfirmation?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(_confirmation?.UserId == userId ? _confirmation : null); public Task AddAsync(OnboardingConfirmation confirmation, CancellationToken cancellationToken) => Task.CompletedTask; public Task UpdateAsync(OnboardingConfirmation confirmation, CancellationToken cancellationToken) => Task.CompletedTask; }
}
