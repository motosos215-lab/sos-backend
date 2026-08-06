using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MotoSOS.API.Modules.Auth.Application;
using MotoSOS.API.Modules.Auth.Contracts;
using MotoSOS.API.Modules.Auth.Domain;
using MotoSOS.API.Modules.Devices.Application;
using MotoSOS.API.Modules.Devices.Domain;
using MotoSOS.API.Modules.EmergencyContacts.Application;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
using MotoSOS.API.Modules.Onboarding.Application;
using MotoSOS.API.Modules.Onboarding.Domain;
using MotoSOS.API.Modules.Plans.Application;
using MotoSOS.API.Modules.Plans.Domain;
using MotoSOS.API.Modules.Profiles.Application;
using MotoSOS.API.Modules.Profiles.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;
using MotoSOS.API.Modules.Vehicles.Application;
using MotoSOS.API.Modules.Vehicles.Domain;

namespace SecurityTest;

public sealed class OnboardingConfirmationSecurityTests
{
    [Fact]
    public async Task SummaryDoesNotExposeSecretsIdentifiersOrPaymentData()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        User user = await AuthenticateAsync(client, "confirm-safe@example.com", stores);
        SeedReadyState(stores, user.Id);

        string body = await (await client.GetAsync("/api/v1/onboarding/summary")).Content.ReadAsStringAsync();

        body.Should().NotContain("passwordHash");
        body.Should().NotContain("refreshToken");
        body.Should().NotContain("deviceIdentifier");
        body.Should().NotContain("deviceIdentifierHash");
        body.Should().NotContain("local-device-id");
        body.Should().NotContain("GooglePlay");
        body.Should().NotContain("Stripe");
        body.Should().NotContain("Payment");
    }

    [Fact]
    public async Task ConfirmDoesNotChangeUserSecurityFields()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        User user = await AuthenticateAsync(client, "confirm-immutable@example.com", stores);
        SeedReadyState(stores, user.Id);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/onboarding/confirm", new { email = "attacker@example.com", role = "Admin", isActive = false, permissions = "admin" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        user.Email.Should().Be("confirm-immutable@example.com");
        user.Role.Should().Be(UserRole.Rider);
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmationDoesNotMakeOtherUserOperationalAndNotReadyDoesNotActivate()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient first = factory.CreateClient();
        HttpClient second = factory.CreateClient();
        User owner = await AuthenticateAsync(first, "confirm-owner@example.com", stores);
        SeedReadyState(stores, owner.Id);
        await first.PostAsync("/api/v1/onboarding/confirm", null);
        await AuthenticateAsync(second, "confirm-other@example.com", stores);

        string otherSummary = await (await second.GetAsync("/api/v1/onboarding/summary")).Content.ReadAsStringAsync();
        HttpResponseMessage otherConfirm = await second.PostAsync("/api/v1/onboarding/confirm", null);

        otherSummary.Should().Contain("\"isOperational\":false");
        otherSummary.Should().NotContain(owner.Id);
        otherConfirm.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        stores.Confirmations.Confirmations.Should().ContainSingle(confirmation => confirmation.UserId == owner.Id && confirmation.IsOperational);
    }

    private static void SeedReadyState(TestStores stores, string userId)
    {
        stores.Profiles.Profiles.Add(new DriverProfile { UserId = userId, CompletionStatus = ProfileCompletionStatus.Completed, PrimaryCity = "Toluca", AddressOrZone = "Zona Centro" });
        stores.Vehicles.Vehicles.Add(new DriverVehicle { UserId = userId, IsActive = true, CompletionStatus = VehicleCompletionStatus.Completed, VehicleType = VehicleType.Motorcycle, Brand = "Yamaha", Model = "FZ", Year = 2024, Alias = "Mi moto" });
        stores.Contacts.Contacts.Add(new EmergencyContact { UserId = userId, IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Invited, FullName = "Contacto Uno" });
        stores.Devices.Devices.Add(new UserDevice { UserId = userId, IsActive = true, DeviceType = DeviceType.MobileApp, DeviceName = "Motorola Edge", Platform = DevicePlatform.Android, LinkStatus = DeviceLinkStatus.Linked, ConnectionStatus = DeviceConnectionStatus.Online, DeviceIdentifierHash = "local-device-id" });
        stores.Subscriptions.Subscriptions.Add(new UserSubscription { UserId = userId, PlanTier = PlanTier.Basic, Status = SubscriptionStatus.Active, Source = SubscriptionSource.WebBasic });
    }

    private static async Task<User> AuthenticateAsync(HttpClient client, string email, TestStores stores)
    {
        RegisterRequest register = new(email, "StrongPass1!", "StrongPass1!", "Moto Rider", "+52 555 555 5555", "Rider", true);
        await client.PostAsJsonAsync("/api/v1/auth/register", register);
        LoginEnvelope login = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, register.Password))).Content.ReadFromJsonAsync<LoginEnvelope>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Data.AccessToken);
        return stores.Users.Users.Single(user => string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase));
    }

    private static WebApplicationFactory<Program> CreateFactory(TestStores stores) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "MotoSOS",
            ["Jwt:Audience"] = "MotoSOS.Clients",
            ["Jwt:Key"] = new string('K', 48),
            ["Jwt:AccessTokenMinutes"] = "15",
            ["Jwt:RefreshTokenDays"] = "7",
            ["Jwt:RefreshTokenRememberMeDays"] = "30",
            ["MongoDb:ConnectionString"] = string.Empty,
            ["MongoDb:DatabaseName"] = "MotoSOS_Test"
        }));
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IUserRepository>(stores.Users);
            services.AddSingleton<IRefreshTokenRepository>(stores.RefreshTokens);
            services.AddSingleton<IDriverProfileRepository>(stores.Profiles);
            services.AddSingleton<IDriverVehicleRepository>(stores.Vehicles);
            services.AddSingleton<IEmergencyContactRepository>(stores.Contacts);
            services.AddSingleton<IUserDeviceRepository>(stores.Devices);
            services.AddSingleton<IUserSubscriptionRepository>(stores.Subscriptions);
            services.AddSingleton<IOnboardingConfirmationRepository>(stores.Confirmations);
        });
    });

    private sealed class TestStores { public InMemoryUserRepository Users { get; } = new(); public InMemoryRefreshTokenRepository RefreshTokens { get; } = new(); public InMemoryDriverProfileRepository Profiles { get; } = new(); public InMemoryDriverVehicleRepository Vehicles { get; } = new(); public InMemoryEmergencyContactRepository Contacts { get; } = new(); public InMemoryUserDeviceRepository Devices { get; } = new(); public InMemoryUserSubscriptionRepository Subscriptions { get; } = new(); public InMemoryOnboardingConfirmationRepository Confirmations { get; } = new(); }
    private sealed class InMemoryUserRepository : IUserRepository { public List<User> Users { get; } = []; public Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(Users.FirstOrDefault(user => user.Id == id)); public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) => Task.FromResult(Users.FirstOrDefault(user => string.Equals(user.Email, email.Trim(), StringComparison.OrdinalIgnoreCase))); public Task AddAsync(User user, CancellationToken cancellationToken) { Users.Add(user); return Task.CompletedTask; } public Task UpdateAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryRefreshTokenRepository : IRefreshTokenRepository { public List<RefreshToken> Tokens { get; } = []; public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) => Task.FromResult(Tokens.FirstOrDefault(token => token.TokenHash == tokenHash)); public Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken) { Tokens.Add(refreshToken); return Task.CompletedTask; } public Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryDriverProfileRepository : IDriverProfileRepository { public List<DriverProfile> Profiles { get; } = []; public Task<DriverProfile?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Profiles.FirstOrDefault(profile => profile.UserId == userId)); public Task AddAsync(DriverProfile profile, CancellationToken cancellationToken) { Profiles.Add(profile); return Task.CompletedTask; } public Task UpdateAsync(DriverProfile profile, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryDriverVehicleRepository : IDriverVehicleRepository { public List<DriverVehicle> Vehicles { get; } = []; public Task<IReadOnlyList<DriverVehicle>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DriverVehicle>>(Vehicles.Where(vehicle => vehicle.UserId == userId && vehicle.IsActive).ToArray()); public Task<DriverVehicle?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(Vehicles.FirstOrDefault(vehicle => vehicle.Id == id)); public Task<int> CountActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Vehicles.Count(vehicle => vehicle.UserId == userId && vehicle.IsActive)); public Task AddAsync(DriverVehicle vehicle, CancellationToken cancellationToken) { Vehicles.Add(vehicle); return Task.CompletedTask; } public Task UpdateAsync(DriverVehicle vehicle, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryEmergencyContactRepository : IEmergencyContactRepository { public List<EmergencyContact> Contacts { get; } = []; public Task<IReadOnlyList<EmergencyContact>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<EmergencyContact>>(Contacts.Where(contact => contact.UserId == userId && contact.IsActive).ToArray()); public Task<EmergencyContact?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(Contacts.FirstOrDefault(contact => contact.Id == id)); public Task<EmergencyContact?> GetByLinkingCodeAsync(string linkingCode, CancellationToken cancellationToken) => Task.FromResult(Contacts.FirstOrDefault(contact => contact.LinkingCode == linkingCode)); public Task<int> CountActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Contacts.Count(contact => contact.UserId == userId && contact.IsActive)); public Task AddAsync(EmergencyContact contact, CancellationToken cancellationToken) { Contacts.Add(contact); return Task.CompletedTask; } public Task UpdateAsync(EmergencyContact contact, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryUserDeviceRepository : IUserDeviceRepository { public List<UserDevice> Devices { get; } = []; public Task<IReadOnlyList<UserDevice>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<UserDevice>>(Devices.Where(device => device.UserId == userId && device.IsActive).ToArray()); public Task<IReadOnlyList<UserDevice>> GetActiveByParentDeviceIdAsync(string parentDeviceId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<UserDevice>>([]); public Task<UserDevice?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult<UserDevice?>(null); public Task<UserDevice?> GetByDeviceIdentifierHashAsync(string userId, string hash, DeviceType deviceType, CancellationToken cancellationToken) => Task.FromResult<UserDevice?>(null); public Task<int> CountActiveLinkedByUserIdAndTypeAsync(string userId, DeviceType deviceType, CancellationToken cancellationToken) => Task.FromResult(0); public Task<bool> HasActiveLinkedMobileAppAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Devices.Any(device => device.UserId == userId && device.DeviceType == DeviceType.MobileApp && device.IsActive && device.LinkStatus == DeviceLinkStatus.Linked)); public Task AddAsync(UserDevice device, CancellationToken cancellationToken) { Devices.Add(device); return Task.CompletedTask; } public Task UpdateAsync(UserDevice device, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryUserSubscriptionRepository : IUserSubscriptionRepository { public List<UserSubscription> Subscriptions { get; } = []; public Task<UserSubscription?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Subscriptions.FirstOrDefault(subscription => subscription.UserId == userId)); public Task<bool> HasActiveSubscriptionAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Subscriptions.Any(subscription => subscription.UserId == userId && subscription.Status == SubscriptionStatus.Active)); public Task AddAsync(UserSubscription subscription, CancellationToken cancellationToken) { Subscriptions.Add(subscription); return Task.CompletedTask; } public Task UpdateAsync(UserSubscription subscription, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryOnboardingConfirmationRepository : IOnboardingConfirmationRepository { public List<OnboardingConfirmation> Confirmations { get; } = []; public Task<OnboardingConfirmation?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Confirmations.FirstOrDefault(confirmation => confirmation.UserId == userId)); public Task AddAsync(OnboardingConfirmation confirmation, CancellationToken cancellationToken) { Confirmations.Add(confirmation); return Task.CompletedTask; } public Task UpdateAsync(OnboardingConfirmation confirmation, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed record LoginEnvelope(bool Success, LoginResponse Data);
}
