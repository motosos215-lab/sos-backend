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
using MotoSOS.API.Modules.EmergencyContacts.Contracts;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
using MotoSOS.API.Modules.Plans.Application;
using MotoSOS.API.Modules.Plans.Domain;
using MotoSOS.API.Modules.Profiles.Application;
using MotoSOS.API.Modules.Profiles.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;
using MotoSOS.API.Modules.Vehicles.Application;
using MotoSOS.API.Modules.Vehicles.Domain;

namespace IntegrationTest;

public sealed class EmergencyContactEndpointsTests
{
    [Fact]
    public async Task EmergencyContactsRequireAuthentication()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage getResponse = await client.GetAsync("/api/v1/emergency-contacts");
        HttpResponseMessage postResponse = await client.PostAsJsonAsync("/api/v1/emergency-contacts", ValidCreateRequest());

        getResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        postResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RiderCanCreateDraftAndContinueAndList()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        await AuthenticateAsync(client, "contact-list@example.com", "Rider");

        HttpResponseMessage draft = await client.PostAsJsonAsync("/api/v1/emergency-contacts", DraftCreateRequest());
        stores.Contacts.Contacts.Clear();
        HttpResponseMessage pending = await client.PostAsJsonAsync("/api/v1/emergency-contacts", ValidCreateRequest());
        HttpResponseMessage list = await client.GetAsync("/api/v1/emergency-contacts");
        string content = await list.Content.ReadAsStringAsync();

        draft.StatusCode.Should().Be(HttpStatusCode.Created);
        pending.StatusCode.Should().Be(HttpStatusCode.Created);
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("Maria Lopez");
        content.Should().Contain("\"invitationStatus\":\"Pending\"");
        content.Should().Contain("\"isPrimary\":true");
    }

    [Fact]
    public async Task RiderCanGetUpdateDeleteAndInviteOwnContact()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        await AuthenticateAsync(client, "contact-crud@example.com", "Rider");
        ContactEnvelope created = await CreateContactAsync(client);

        HttpResponseMessage get = await client.GetAsync($"/api/v1/emergency-contacts/{created.Data.Contact.Id}");
        HttpResponseMessage update = await client.PutAsJsonAsync($"/api/v1/emergency-contacts/{created.Data.Contact.Id}", ValidUpdateRequest());
        HttpResponseMessage invite = await client.PostAsync($"/api/v1/emergency-contacts/{created.Data.Contact.Id}/invite", null);
        InviteEnvelope? inviteBody = await invite.Content.ReadFromJsonAsync<InviteEnvelope>();
        HttpResponseMessage invitation = await client.GetAsync($"/api/v1/emergency-contacts/invitations/{inviteBody!.Data.Contact.LinkingCode}");
        HttpResponseMessage delete = await client.DeleteAsync($"/api/v1/emergency-contacts/{created.Data.Contact.Id}");

        get.StatusCode.Should().Be(HttpStatusCode.OK);
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        invite.StatusCode.Should().Be(HttpStatusCode.OK);
        inviteBody.Data.Contact.InvitationStatus.Should().Be("Invited");
        invitation.StatusCode.Should().Be(HttpStatusCode.OK);
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        stores.Contacts.Contacts.Single(contact => contact.Id == created.Data.Contact.Id).InvitationStatus.Should().Be(EmergencyContactInvitationStatus.Revoked);
    }

    [Fact]
    public async Task InviteRegeneratesDifferentCode()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        await AuthenticateAsync(client, "contact-reinvite@example.com", "Rider");
        ContactEnvelope created = await CreateContactAsync(client);

        InviteEnvelope first = (await (await client.PostAsync($"/api/v1/emergency-contacts/{created.Data.Contact.Id}/invite", null)).Content.ReadFromJsonAsync<InviteEnvelope>())!;
        InviteEnvelope second = (await (await client.PostAsync($"/api/v1/emergency-contacts/{created.Data.Contact.Id}/invite", null)).Content.ReadFromJsonAsync<InviteEnvelope>())!;

        second.Data.Contact.LinkingCode.Should().NotBe(first.Data.Contact.LinkingCode);
    }

    [Fact]
    public async Task RiderCannotAccessOtherUsersContact()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient firstClient = factory.CreateClient();
        HttpClient secondClient = factory.CreateClient();
        await AuthenticateAsync(firstClient, "contact-owner@example.com", "Rider");
        ContactEnvelope created = await CreateContactAsync(firstClient);
        await AuthenticateAsync(secondClient, "contact-other@example.com", "Rider");

        HttpResponseMessage response = await secondClient.GetAsync($"/api/v1/emergency-contacts/{created.Data.Contact.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("Monitor")]
    [InlineData("Admin")]
    public async Task NonRidersReceiveForbidden(string role)
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        if (role == "Admin")
        {
            RegisterRequest register = CreateRegisterRequest("contact-admin@example.com", "Rider");
            await client.PostAsJsonAsync("/api/v1/auth/register", register);
            stores.Users.Users.Single(user => user.Email == register.Email).Role = UserRole.Admin;
            await LoginAsync(client, register.Email, register.Password);
        }
        else
        {
            await AuthenticateAsync(client, "contact-monitor@example.com", "Monitor");
        }

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/emergency-contacts", ValidCreateRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task InvalidContinueAndBasicPlanLimitReturnControlledErrors()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        await AuthenticateAsync(client, "contact-errors@example.com", "Rider");

        HttpResponseMessage invalid = await client.PostAsJsonAsync("/api/v1/emergency-contacts", new CreateEmergencyContactRequest(null, null, null, null, null, null, "Continue"));
        await client.PostAsJsonAsync("/api/v1/emergency-contacts", ValidCreateRequest());
        HttpResponseMessage limit = await client.PostAsJsonAsync("/api/v1/emergency-contacts", ValidCreateRequest() with { FullName = "Otra" });
        string invalidBody = await invalid.Content.ReadAsStringAsync();
        string limitBody = await limit.Content.ReadAsStringAsync();

        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        invalidBody.Should().Contain("validation_error");
        limit.StatusCode.Should().Be(HttpStatusCode.Conflict);
        limitBody.Should().Contain("plan_limit_exceeded");
    }

    [Fact]
    public async Task OnboardingAdvancesToFourStepsAfterInvitedContact()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        await AuthenticateAsync(client, "contact-onboarding@example.com", "Rider");
        User user = stores.Users.Users.Single(user => user.Email == "contact-onboarding@example.com");
        stores.DriverProfiles.Profiles.Add(new DriverProfile { UserId = user.Id, CompletionStatus = ProfileCompletionStatus.Completed });
        stores.DriverVehicles.Vehicles.Add(new DriverVehicle { UserId = user.Id, IsActive = true, CompletionStatus = VehicleCompletionStatus.Completed });
        ContactEnvelope created = await CreateContactAsync(client);
        HttpResponseMessage inProgress = await client.GetAsync("/api/v1/onboarding/status");
        await client.PostAsync($"/api/v1/emergency-contacts/{created.Data.Contact.Id}/invite", null);

        HttpResponseMessage completed = await client.GetAsync("/api/v1/onboarding/status");
        string completedBody = await completed.Content.ReadAsStringAsync();

        inProgress.StatusCode.Should().Be(HttpStatusCode.OK);
        completed.StatusCode.Should().Be(HttpStatusCode.OK);
        completedBody.Should().Contain("\"completedSteps\":4");
        completedBody.Should().Contain("\"progressPercentage\":57");
        completedBody.Should().Contain("\"currentStep\":\"Devices\"");
        completedBody.Should().Contain("\"key\":\"EmergencyContacts\"");
        completedBody.Should().Contain("\"status\":\"Completed\"");
    }

    private static async Task<ContactEnvelope> CreateContactAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/emergency-contacts", ValidCreateRequest());
        ContactEnvelope? envelope = await response.Content.ReadFromJsonAsync<ContactEnvelope>();
        envelope.Should().NotBeNull();
        return envelope!;
    }

    private static async Task AuthenticateAsync(HttpClient client, string email, string accountType)
    {
        RegisterRequest register = CreateRegisterRequest(email, accountType);
        await client.PostAsJsonAsync("/api/v1/auth/register", register);
        await LoginAsync(client, register.Email, register.Password);
    }

    private static async Task LoginAsync(HttpClient client, string email, string password)
    {
        HttpResponseMessage loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        LoginEnvelope? login = await loginResponse.Content.ReadFromJsonAsync<LoginEnvelope>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Data.AccessToken);
    }

    private static RegisterRequest CreateRegisterRequest(string email, string accountType) => new(email, "StrongPass1!", "StrongPass1!", "Moto Rider", "+52 555 555 5555", accountType, true);
    private static CreateEmergencyContactRequest DraftCreateRequest() => new("Maria", null, null, null, null, null, "Draft");
    private static CreateEmergencyContactRequest ValidCreateRequest() => new("Maria Lopez", "Esposa", "+52 5512345678", "maria@example.com", 1, new EmergencyContactPermissionsRequest(true, true, false, false), "Continue");
    private static UpdateEmergencyContactRequest ValidUpdateRequest() => new("Maria Lopez", "Hermana", "+52 5512345678", "maria@example.com", 1, new EmergencyContactPermissionsRequest(true, true, true, false), "Continue");

    private static WebApplicationFactory<Program> CreateFactory(TestStores stores) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "MotoSOS",
            ["Jwt:Audience"] = "MotoSOS.Clients",
            ["Jwt:Key"] = new string('E', 48),
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
            services.AddSingleton<IDriverProfileRepository>(stores.DriverProfiles);
            services.AddSingleton<IDriverVehicleRepository>(stores.DriverVehicles);
            services.AddSingleton<IEmergencyContactRepository>(stores.Contacts);
            services.AddSingleton<IUserDeviceRepository>(stores.Devices);
            services.AddSingleton<IUserSubscriptionRepository>(stores.Subscriptions);
        });
    });

    private sealed class TestStores
    {
        public InMemoryUserRepository Users { get; } = new();
        public InMemoryRefreshTokenRepository RefreshTokens { get; } = new();
        public InMemoryDriverProfileRepository DriverProfiles { get; } = new();
        public InMemoryDriverVehicleRepository DriverVehicles { get; } = new();
        public InMemoryEmergencyContactRepository Contacts { get; } = new();
        public InMemoryUserDeviceRepository Devices { get; } = new();
        public InMemoryUserSubscriptionRepository Subscriptions { get; } = new();
    }
    private sealed class InMemoryUserRepository : IUserRepository
    {
        public List<User> Users { get; } = [];
        public Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(Users.FirstOrDefault(user => user.Id == id));
        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) => Task.FromResult(Users.FirstOrDefault(user => string.Equals(user.Email, email.Trim(), StringComparison.OrdinalIgnoreCase)));
        public Task AddAsync(User user, CancellationToken cancellationToken) { Users.Add(user); return Task.CompletedTask; }
        public Task UpdateAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask;
    }
    private sealed class InMemoryRefreshTokenRepository : IRefreshTokenRepository
    {
        public List<RefreshToken> Tokens { get; } = [];
        public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) => Task.FromResult(Tokens.FirstOrDefault(token => token.TokenHash == tokenHash));
        public Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken) { Tokens.Add(refreshToken); return Task.CompletedTask; }
        public Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken) => Task.CompletedTask;
    }
    private sealed class InMemoryDriverProfileRepository : IDriverProfileRepository
    {
        public List<DriverProfile> Profiles { get; } = [];
        public Task<DriverProfile?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Profiles.FirstOrDefault(profile => profile.UserId == userId));
        public Task AddAsync(DriverProfile profile, CancellationToken cancellationToken) { Profiles.Add(profile); return Task.CompletedTask; }
        public Task UpdateAsync(DriverProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
    }
    private sealed class InMemoryDriverVehicleRepository : IDriverVehicleRepository
    {
        public List<DriverVehicle> Vehicles { get; } = [];
        public Task<IReadOnlyList<DriverVehicle>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DriverVehicle>>(Vehicles.Where(vehicle => vehicle.UserId == userId && vehicle.IsActive).ToArray());
        public Task<DriverVehicle?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(Vehicles.FirstOrDefault(vehicle => vehicle.Id == id));
        public Task<int> CountActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Vehicles.Count(vehicle => vehicle.UserId == userId && vehicle.IsActive));
        public Task AddAsync(DriverVehicle vehicle, CancellationToken cancellationToken) { Vehicles.Add(vehicle); return Task.CompletedTask; }
        public Task UpdateAsync(DriverVehicle vehicle, CancellationToken cancellationToken) => Task.CompletedTask;
    }
    private sealed class InMemoryEmergencyContactRepository : IEmergencyContactRepository
    {
        public List<EmergencyContact> Contacts { get; } = [];
        public Task<IReadOnlyList<EmergencyContact>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<EmergencyContact>>(Contacts.Where(contact => contact.UserId == userId && contact.IsActive).ToArray());
        public Task<EmergencyContact?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(Contacts.FirstOrDefault(contact => contact.Id == id));
        public Task<EmergencyContact?> GetByLinkingCodeAsync(string linkingCode, CancellationToken cancellationToken) => Task.FromResult(Contacts.FirstOrDefault(contact => contact.LinkingCode == linkingCode));
        public Task<int> CountActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Contacts.Count(contact => contact.UserId == userId && contact.IsActive));
        public Task AddAsync(EmergencyContact contact, CancellationToken cancellationToken) { Contacts.Add(contact); return Task.CompletedTask; }
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
    private sealed class InMemoryUserSubscriptionRepository : IUserSubscriptionRepository
    {
        public List<UserSubscription> Subscriptions { get; } = [];
        public Task<UserSubscription?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Subscriptions.FirstOrDefault(subscription => subscription.UserId == userId));
        public Task<bool> HasActiveSubscriptionAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Subscriptions.Any(subscription => subscription.UserId == userId && subscription.Status == SubscriptionStatus.Active));
        public Task AddAsync(UserSubscription subscription, CancellationToken cancellationToken) { Subscriptions.Add(subscription); return Task.CompletedTask; }
        public Task UpdateAsync(UserSubscription subscription, CancellationToken cancellationToken) => Task.CompletedTask;
    }
    private sealed record LoginEnvelope(bool Success, LoginData Data);
    private sealed record LoginData(string AccessToken, string RefreshToken);
    private sealed record ContactEnvelope(bool Success, CreateEmergencyContactResponse Data);
    private sealed record InviteEnvelope(bool Success, InviteEmergencyContactResponse Data);
}
