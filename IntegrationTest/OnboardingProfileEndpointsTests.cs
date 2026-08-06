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
using MotoSOS.API.Modules.Onboarding.Contracts;
using MotoSOS.API.Modules.Profiles.Application;
using MotoSOS.API.Modules.Profiles.Contracts;
using MotoSOS.API.Modules.Profiles.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;
using MotoSOS.API.Modules.Vehicles.Application;
using MotoSOS.API.Modules.Vehicles.Domain;

namespace IntegrationTest;

public sealed class OnboardingProfileEndpointsTests
{
    [Fact]
    public async Task OnboardingStatusRequiresAuthentication()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/onboarding/status");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OnboardingStatusForRiderReturnsAccountCompletedAndProfilePending()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        await AuthenticateAsync(client, "rider-status@example.com", "Rider");

        HttpResponseMessage response = await client.GetAsync("/api/v1/onboarding/status");
        string content = await response.Content.ReadAsStringAsync();
        OnboardingEnvelope? envelope = await response.Content.ReadFromJsonAsync<OnboardingEnvelope>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        envelope.Should().NotBeNull();
        AssertWizardSteps(envelope!.Data.Steps);
        envelope.Data.Steps.Should().Contain(step => step.Key == "Account" && step.Status == "Completed");
        envelope.Data.Steps.Should().Contain(step => step.Key == "Profile" && step.Status == "Pending");
        content.Should().Contain("\"completedSteps\":1");
        content.Should().Contain("\"progressPercentage\":14");
        content.Should().Contain("\"key\":\"Account\"");
        content.Should().Contain("\"status\":\"Completed\"");
        content.Should().Contain("\"key\":\"Profile\"");
        content.Should().Contain("\"status\":\"Pending\"");
    }

    [Fact]
    public async Task ProfileRequiresAuthentication()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/profiles/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProfileReturnsInitialObjectWhenMissing()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        await AuthenticateAsync(client, "missing-profile@example.com", "Rider");

        HttpResponseMessage response = await client.GetAsync("/api/v1/profiles/me");
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("\"profile\":");
        content.Should().Contain("\"id\":null");
        content.Should().Contain("\"completionStatus\":\"Draft\"");
        content.Should().Contain("\"licenseDocumentStatus\":\"NotUploaded\"");
    }

    [Fact]
    public async Task DraftUpsertCreatesProfile()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        await AuthenticateAsync(client, "draft-profile@example.com", "Rider");
        var request = new UpsertMyProfileRequest("Draft Rider", null, null, null, "Centro", null, null, null, null, null, null, "Draft");

        HttpResponseMessage response = await client.PutAsJsonAsync("/api/v1/profiles/me", request);
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("\"completionStatus\":\"Draft\"");
        stores.DriverProfiles.Profiles.Should().ContainSingle();
    }

    [Fact]
    public async Task OnboardingStatusAfterDraftProfileReturnsProfileInProgressAndSevenSteps()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        await AuthenticateAsync(client, "draft-onboarding@example.com", "Rider");
        var request = new UpsertMyProfileRequest("Draft Rider", null, null, null, "Centro", null, null, null, null, null, null, "Draft");
        await client.PutAsJsonAsync("/api/v1/profiles/me", request);

        HttpResponseMessage response = await client.GetAsync("/api/v1/onboarding/status");
        OnboardingEnvelope? envelope = await response.Content.ReadFromJsonAsync<OnboardingEnvelope>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        envelope.Should().NotBeNull();
        AssertWizardSteps(envelope!.Data.Steps);
        envelope.Data.CompletedSteps.Should().Be(1);
        envelope.Data.ProgressPercentage.Should().Be(14);
        envelope.Data.CurrentStep.Should().Be("Profile");
        envelope.Data.Steps.Should().Contain(step => step.Key == "Profile" && step.Status == "InProgress");
    }

    [Fact]
    public async Task ContinueUpsertCompletesProfile()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        await AuthenticateAsync(client, "complete-profile@example.com", "Rider");

        HttpResponseMessage response = await client.PutAsJsonAsync("/api/v1/profiles/me", ValidContinueRequest());
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("\"completionStatus\":\"Completed\"");
        content.Should().Contain("\"completedAtUtc\":");
        stores.DriverProfiles.Profiles.Should().ContainSingle(profile => profile.CompletionStatus == ProfileCompletionStatus.Completed);
    }

    [Fact]
    public async Task OnboardingStatusAfterCompletedProfileReturnsTwoStepsAndTwentyNinePercent()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        await AuthenticateAsync(client, "completed-onboarding@example.com", "Rider");
        await client.PutAsJsonAsync("/api/v1/profiles/me", ValidContinueRequest());

        HttpResponseMessage response = await client.GetAsync("/api/v1/onboarding/status");
        string content = await response.Content.ReadAsStringAsync();
        OnboardingEnvelope? envelope = await response.Content.ReadFromJsonAsync<OnboardingEnvelope>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        envelope.Should().NotBeNull();
        AssertWizardSteps(envelope!.Data.Steps);
        envelope.Data.Steps.Should().Contain(step => step.Key == "Profile" && step.Status == "Completed");
        envelope.Data.Steps.Should().Contain(step => step.Key == "Vehicle" && step.Status == "Pending");
        content.Should().Contain("\"completedSteps\":2");
        content.Should().Contain("\"progressPercentage\":29");
        content.Should().Contain("\"currentStep\":\"Vehicle\"");
    }

    [Fact]
    public async Task OnboardingStatusAfterDraftVehicleReturnsVehicleInProgress()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        await AuthenticateAsync(client, "draft-vehicle-onboarding@example.com", "Rider");
        User user = stores.Users.Users.Single(user => user.Email == "draft-vehicle-onboarding@example.com");
        stores.DriverProfiles.Profiles.Add(new DriverProfile { UserId = user.Id, CompletionStatus = ProfileCompletionStatus.Completed });
        stores.DriverVehicles.Vehicles.Add(new DriverVehicle { UserId = user.Id, IsActive = true, CompletionStatus = VehicleCompletionStatus.Draft });

        HttpResponseMessage response = await client.GetAsync("/api/v1/onboarding/status");
        OnboardingEnvelope? envelope = await response.Content.ReadFromJsonAsync<OnboardingEnvelope>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        envelope.Should().NotBeNull();
        envelope!.Data.CompletedSteps.Should().Be(2);
        envelope.Data.ProgressPercentage.Should().Be(29);
        envelope.Data.CurrentStep.Should().Be("Vehicle");
        envelope.Data.Steps.Should().Contain(step => step.Key == "Vehicle" && step.Status == "InProgress");
    }

    [Fact]
    public async Task OnboardingStatusAfterCompletedVehicleReturnsThreeStepsAndEmergencyContacts()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        await AuthenticateAsync(client, "completed-vehicle-onboarding@example.com", "Rider");
        User user = stores.Users.Users.Single(user => user.Email == "completed-vehicle-onboarding@example.com");
        stores.DriverProfiles.Profiles.Add(new DriverProfile { UserId = user.Id, CompletionStatus = ProfileCompletionStatus.Completed });
        stores.DriverVehicles.Vehicles.Add(new DriverVehicle { UserId = user.Id, IsActive = true, CompletionStatus = VehicleCompletionStatus.Completed });

        HttpResponseMessage response = await client.GetAsync("/api/v1/onboarding/status");
        OnboardingEnvelope? envelope = await response.Content.ReadFromJsonAsync<OnboardingEnvelope>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        envelope.Should().NotBeNull();
        envelope!.Data.CompletedSteps.Should().Be(3);
        envelope.Data.ProgressPercentage.Should().Be(43);
        envelope.Data.CurrentStep.Should().Be("EmergencyContacts");
        envelope.Data.Steps.Should().Contain(step => step.Key == "Vehicle" && step.Status == "Completed");
    }

    [Fact]
    public async Task MonitorCannotUseDriverOnboardingOrCompleteProfile()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        await AuthenticateAsync(client, "monitor-flow@example.com", "Monitor");

        HttpResponseMessage statusResponse = await client.GetAsync("/api/v1/onboarding/status");
        HttpResponseMessage getProfileResponse = await client.GetAsync("/api/v1/profiles/me");
        HttpResponseMessage profileResponse = await client.PutAsJsonAsync("/api/v1/profiles/me", ValidContinueRequest());

        statusResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        getProfileResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        profileResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminCannotUseDriverOnboardingOrCompleteProfile()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        RegisterRequest register = CreateRegisterRequest("admin-flow@example.com", "Rider");
        await client.PostAsJsonAsync("/api/v1/auth/register", register);
        stores.Users.Users.Single(user => user.Email == register.Email).Role = UserRole.Admin;
        HttpResponseMessage loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(register.Email, register.Password));
        LoginEnvelope? login = await loginResponse.Content.ReadFromJsonAsync<LoginEnvelope>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Data.AccessToken);

        HttpResponseMessage statusResponse = await client.GetAsync("/api/v1/onboarding/status");
        HttpResponseMessage getProfileResponse = await client.GetAsync("/api/v1/profiles/me");
        HttpResponseMessage profileResponse = await client.PutAsJsonAsync("/api/v1/profiles/me", ValidContinueRequest());

        statusResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        getProfileResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        profileResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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

    private static async Task AuthenticateAsync(HttpClient client, string email, string accountType)
    {
        RegisterRequest register = CreateRegisterRequest(email, accountType);
        await client.PostAsJsonAsync("/api/v1/auth/register", register);
        HttpResponseMessage loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(register.Email, register.Password));
        LoginEnvelope? login = await loginResponse.Content.ReadFromJsonAsync<LoginEnvelope>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Data.AccessToken);
    }

    private static RegisterRequest CreateRegisterRequest(string email, string accountType) =>
        new(email, "StrongPass1!", "StrongPass1!", "Moto Rider", "+52 555 555 5555", accountType, true);

    private static UpsertMyProfileRequest ValidContinueRequest() => new(
        "Moto Rider Updated",
        "+52 555 555 5555",
        new DateOnly(1995, 1, 15),
        "optional",
        "Colonia Centro",
        "Toluca",
        "O+",
        "Ninguna",
        "Ninguna",
        "Contacto Principal",
        "+52 555 111 2233",
        "Continue");

    private static WebApplicationFactory<Program> CreateFactory(TestStores stores)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Issuer"] = "MotoSOS",
                    ["Jwt:Audience"] = "MotoSOS.Clients",
                    ["Jwt:Key"] = new string('P', 48),
                    ["Jwt:AccessTokenMinutes"] = "15",
                    ["Jwt:RefreshTokenDays"] = "7",
                    ["Jwt:RefreshTokenRememberMeDays"] = "30",
                    ["MongoDb:ConnectionString"] = string.Empty,
                    ["MongoDb:DatabaseName"] = "MotoSOS_Test"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IUserRepository>(stores.Users);
                services.AddSingleton<IRefreshTokenRepository>(stores.RefreshTokens);
                services.AddSingleton<IDriverProfileRepository>(stores.DriverProfiles);
                services.AddSingleton<IDriverVehicleRepository>(stores.DriverVehicles);
                services.AddSingleton<IEmergencyContactRepository>(stores.EmergencyContacts);
                services.AddSingleton<IUserDeviceRepository>(stores.Devices);
            });
        });
    }

    private sealed class TestStores
    {
        public InMemoryUserRepository Users { get; } = new();

        public InMemoryRefreshTokenRepository RefreshTokens { get; } = new();

        public InMemoryDriverProfileRepository DriverProfiles { get; } = new();

        public InMemoryDriverVehicleRepository DriverVehicles { get; } = new();

        public InMemoryEmergencyContactRepository EmergencyContacts { get; } = new();

        public InMemoryUserDeviceRepository Devices { get; } = new();
    }

    private sealed class InMemoryUserRepository : IUserRepository
    {
        public List<User> Users { get; } = [];

        public Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(Users.FirstOrDefault(user => user.Id == id));

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult(Users.FirstOrDefault(user => string.Equals(user.Email, email.Trim(), StringComparison.OrdinalIgnoreCase)));

        public Task AddAsync(User user, CancellationToken cancellationToken)
        {
            Users.Add(user);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryRefreshTokenRepository : IRefreshTokenRepository
    {
        public List<RefreshToken> Tokens { get; } = [];

        public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
            Task.FromResult(Tokens.FirstOrDefault(token => token.TokenHash == tokenHash));

        public Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
        {
            Tokens.Add(refreshToken);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryDriverProfileRepository : IDriverProfileRepository
    {
        public List<DriverProfile> Profiles { get; } = [];

        public Task<DriverProfile?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult(Profiles.FirstOrDefault(profile => profile.UserId == userId));

        public Task AddAsync(DriverProfile profile, CancellationToken cancellationToken)
        {
            Profiles.Add(profile);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(DriverProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryDriverVehicleRepository : IDriverVehicleRepository
    {
        public List<DriverVehicle> Vehicles { get; } = [];

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

    private sealed class InMemoryEmergencyContactRepository : IEmergencyContactRepository
    {
        public List<EmergencyContact> Contacts { get; } = [];

        public Task<IReadOnlyList<EmergencyContact>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EmergencyContact>>(Contacts.Where(contact => contact.UserId == userId && contact.IsActive).ToArray());

        public Task<EmergencyContact?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(Contacts.FirstOrDefault(contact => contact.Id == id));

        public Task<EmergencyContact?> GetByLinkingCodeAsync(string linkingCode, CancellationToken cancellationToken) =>
            Task.FromResult(Contacts.FirstOrDefault(contact => contact.LinkingCode == linkingCode));

        public Task<int> CountActiveByUserIdAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult(Contacts.Count(contact => contact.UserId == userId && contact.IsActive));

        public Task AddAsync(EmergencyContact contact, CancellationToken cancellationToken)
        {
            Contacts.Add(contact);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(EmergencyContact contact, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryUserDeviceRepository : IUserDeviceRepository
    {
        public List<UserDevice> Devices { get; } = [];
        public Task<IReadOnlyList<UserDevice>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<UserDevice>>(Devices.Where(device => device.UserId == userId && device.IsActive).ToArray());
        public Task<IReadOnlyList<UserDevice>> GetActiveByParentDeviceIdAsync(string parentDeviceId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<UserDevice>>([]);
        public Task<UserDevice?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult<UserDevice?>(null);
        public Task<UserDevice?> GetByDeviceIdentifierHashAsync(string userId, string hash, DeviceType deviceType, CancellationToken cancellationToken) => Task.FromResult<UserDevice?>(null);
        public Task<int> CountActiveLinkedByUserIdAndTypeAsync(string userId, DeviceType deviceType, CancellationToken cancellationToken) => Task.FromResult(0);
        public Task<bool> HasActiveLinkedMobileAppAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Devices.Any(device => device.UserId == userId && device.DeviceType == DeviceType.MobileApp && device.IsActive && device.LinkStatus == DeviceLinkStatus.Linked));
        public Task AddAsync(UserDevice device, CancellationToken cancellationToken) { Devices.Add(device); return Task.CompletedTask; }
        public Task UpdateAsync(UserDevice device, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed record LoginEnvelope(bool Success, LoginData Data);

    private sealed record LoginData(string AccessToken, string RefreshToken);

    private sealed record OnboardingEnvelope(bool Success, OnboardingStatusResponse Data);
}
