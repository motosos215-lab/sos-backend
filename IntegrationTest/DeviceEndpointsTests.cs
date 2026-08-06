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
using MotoSOS.API.Modules.Devices.Contracts;
using MotoSOS.API.Modules.Devices.Domain;
using MotoSOS.API.Modules.EmergencyContacts.Application;
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

public sealed class DeviceEndpointsTests
{
    [Fact]
    public async Task DevicesRequireAuthentication()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();

        (await client.GetAsync("/api/v1/devices")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsync("/api/v1/devices/mobile/activation-code", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/devices/activation-codes/current")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RiderCanGenerateAndReadCurrentActivationCode()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        await AuthenticateAsync(client, "devices-code@example.com", "Rider", stores);

        HttpResponseMessage empty = await client.GetAsync("/api/v1/devices/activation-codes/current");
        HttpResponseMessage created = await client.PostAsync("/api/v1/devices/mobile/activation-code", null);
        HttpResponseMessage current = await client.GetAsync("/api/v1/devices/activation-codes/current");
        string emptyBody = await empty.Content.ReadAsStringAsync();
        string currentBody = await current.Content.ReadAsStringAsync();

        empty.StatusCode.Should().Be(HttpStatusCode.OK);
        emptyBody.Should().Contain("\"activationCode\":null");
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        current.StatusCode.Should().Be(HttpStatusCode.OK);
        currentBody.Should().Contain("MSOS-");
    }

    [Theory]
    [InlineData("Monitor")]
    [InlineData("Admin")]
    public async Task NonRidersReceiveForbidden(string role)
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        await AuthenticateAsync(client, $"devices-{role}@example.com", role, stores);

        HttpResponseMessage response = await client.PostAsync("/api/v1/devices/mobile/activation-code", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RiderCanLinkMobileListHeartbeatAndRevoke()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        await AuthenticateAsync(client, "devices-mobile@example.com", "Rider", stores);
        CodeEnvelope code = (await (await client.PostAsync("/api/v1/devices/mobile/activation-code", null)).Content.ReadFromJsonAsync<CodeEnvelope>())!;

        HttpResponseMessage link = await client.PostAsJsonAsync("/api/v1/devices/mobile/link", MobileRequest(code.Data.ActivationCode!.Code));
        LinkEnvelope linked = (await link.Content.ReadFromJsonAsync<LinkEnvelope>())!;
        HttpResponseMessage list = await client.GetAsync("/api/v1/devices");
        HttpResponseMessage heartbeat = await client.PatchAsJsonAsync($"/api/v1/devices/{linked.Data.Device.Id}/heartbeat", new HeartbeatDeviceRequest(87, "Online", "1.0.1"));
        HttpResponseMessage revoke = await client.PostAsync($"/api/v1/devices/{linked.Data.Device.Id}/revoke", null);

        link.StatusCode.Should().Be(HttpStatusCode.OK);
        linked.Data.Device.DeviceType.Should().Be("MobileApp");
        linked.Data.Device.IsPrimary.Should().BeTrue();
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        heartbeat.StatusCode.Should().Be(HttpStatusCode.OK);
        revoke.StatusCode.Should().Be(HttpStatusCode.NoContent);
        stores.Devices.Devices.Single(device => device.Id == linked.Data.Device.Id).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ActivationCodeCannotBeUsedByOtherUserOrReusedOrExpired()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient first = factory.CreateClient();
        HttpClient second = factory.CreateClient();
        await AuthenticateAsync(first, "devices-owner@example.com", "Rider", stores);
        CodeEnvelope code = (await (await first.PostAsync("/api/v1/devices/mobile/activation-code", null)).Content.ReadFromJsonAsync<CodeEnvelope>())!;
        await AuthenticateAsync(second, "devices-other@example.com", "Rider", stores);

        HttpResponseMessage otherUse = await second.PostAsJsonAsync("/api/v1/devices/mobile/link", MobileRequest(code.Data.ActivationCode!.Code));
        HttpResponseMessage firstUse = await first.PostAsJsonAsync("/api/v1/devices/mobile/link", MobileRequest(code.Data.ActivationCode.Code));
        stores.Devices.Devices.Clear();
        HttpResponseMessage reused = await first.PostAsJsonAsync("/api/v1/devices/mobile/link", MobileRequest(code.Data.ActivationCode.Code));
        var expired = new DeviceActivationCode { UserId = stores.Users.Users.Single(user => user.Email == "devices-owner@example.com").Id, Code = "MSOS-AAAA-BBBB", ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1) };
        stores.Codes.Codes.Add(expired);
        HttpResponseMessage expiredUse = await first.PostAsJsonAsync("/api/v1/devices/mobile/link", MobileRequest(expired.Code));

        otherUse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        firstUse.StatusCode.Should().Be(HttpStatusCode.OK);
        reused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        expiredUse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CurrentActivationCodeDoesNotReturnExpiredUsedRevokedOrOtherUsersCodes()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        User user = await AuthenticateAsync(client, "devices-current-filter@example.com", "Rider", stores);
        stores.Codes.Codes.Add(new DeviceActivationCode { UserId = "other", Code = "MSOS-OTHR-1111", ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(10) });
        stores.Codes.Codes.Add(new DeviceActivationCode { UserId = user.Id, Code = "MSOS-USED-1111", ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(10), IsUsed = true });
        stores.Codes.Codes.Add(new DeviceActivationCode { UserId = user.Id, Code = "MSOS-REVO-1111", ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(10), IsRevoked = true });
        stores.Codes.Codes.Add(new DeviceActivationCode { UserId = user.Id, Code = "MSOS-EXPR-1111", ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1) });

        string body = await (await client.GetAsync("/api/v1/devices/activation-codes/current")).Content.ReadAsStringAsync();

        body.Should().Contain("\"activationCode\":null");
    }

    [Fact]
    public async Task RiderCanLinkSmartwatchWithOwnMobileAndMobileRevokeRevokesDependent()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        User user = await AuthenticateAsync(client, "devices-watch@example.com", "Rider", stores);
        var mobile = new UserDevice { UserId = user.Id, DeviceType = DeviceType.MobileApp, DeviceName = "Phone", IsActive = true, LinkStatus = DeviceLinkStatus.Linked };
        stores.Devices.Devices.Add(mobile);

        HttpResponseMessage linkWatch = await client.PostAsJsonAsync("/api/v1/devices/smartwatch/link", SmartwatchRequest(mobile.Id));
        WatchEnvelope watch = (await linkWatch.Content.ReadFromJsonAsync<WatchEnvelope>())!;
        HttpResponseMessage revokeMobile = await client.PostAsync($"/api/v1/devices/{mobile.Id}/revoke", null);

        linkWatch.StatusCode.Should().Be(HttpStatusCode.OK);
        watch.Data.Device.ParentDeviceId.Should().Be(mobile.Id);
        revokeMobile.StatusCode.Should().Be(HttpStatusCode.NoContent);
        stores.Devices.Devices.Single(device => device.Id == watch.Data.Device.Id).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task BasicPlanBlocksSecondActiveMobileAppAndOtherUsersDeviceIsNotVisible()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        User user = await AuthenticateAsync(client, "devices-limit@example.com", "Rider", stores);
        stores.Devices.Devices.Add(new UserDevice { UserId = user.Id, DeviceType = DeviceType.MobileApp, DeviceName = "Existing", IsActive = true, LinkStatus = DeviceLinkStatus.Linked });
        stores.Devices.Devices.Add(new UserDevice { UserId = "other", DeviceType = DeviceType.MobileApp, DeviceName = "Other", IsActive = true, LinkStatus = DeviceLinkStatus.Linked });
        stores.Codes.Codes.Add(new DeviceActivationCode { UserId = user.Id, Code = "MSOS-8X7Q-3M2K", ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(10) });

        HttpResponseMessage secondMobile = await client.PostAsJsonAsync("/api/v1/devices/mobile/link", MobileRequest("MSOS-8X7Q-3M2K"));
        string list = await (await client.GetAsync("/api/v1/devices")).Content.ReadAsStringAsync();

        secondMobile.StatusCode.Should().Be(HttpStatusCode.Conflict);
        list.Should().Contain("Existing");
        list.Should().NotContain("Other");
    }

    [Fact]
    public async Task OnboardingMovesFromDevicesToPlanAfterMobileLinked()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        User user = await AuthenticateAsync(client, "devices-onboarding@example.com", "Rider", stores);
        stores.Profiles.Profiles.Add(new DriverProfile { UserId = user.Id, CompletionStatus = ProfileCompletionStatus.Completed });
        stores.Vehicles.Vehicles.Add(new DriverVehicle { UserId = user.Id, IsActive = true, CompletionStatus = VehicleCompletionStatus.Completed });
        stores.Contacts.Contacts.Add(new EmergencyContact { UserId = user.Id, IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Invited });

        string before = await (await client.GetAsync("/api/v1/onboarding/status")).Content.ReadAsStringAsync();
        CodeEnvelope code = (await (await client.PostAsync("/api/v1/devices/mobile/activation-code", null)).Content.ReadFromJsonAsync<CodeEnvelope>())!;
        await client.PostAsJsonAsync("/api/v1/devices/mobile/link", MobileRequest(code.Data.ActivationCode!.Code));
        string after = await (await client.GetAsync("/api/v1/onboarding/status")).Content.ReadAsStringAsync();

        before.Should().Contain("\"completedSteps\":4");
        before.Should().Contain("\"currentStep\":\"Devices\"");
        after.Should().Contain("\"completedSteps\":5");
        after.Should().Contain("\"progressPercentage\":71");
        after.Should().Contain("\"currentStep\":\"Plan\"");
    }

    private static LinkMobileDeviceRequest MobileRequest(string code) => new(code, "Motorola Edge", "Android", "Motorola", "Edge 40", "14", "1.0.0", Guid.NewGuid().ToString());
    private static LinkSmartwatchRequest SmartwatchRequest(string parentId) => new(parentId, "Galaxy Watch", "WearOS", "Samsung", "Galaxy Watch 6", "Wear OS 4", "1.0.0", "local-watch-id", 80);

    private static async Task<User> AuthenticateAsync(HttpClient client, string email, string accountType, TestStores stores)
    {
        RegisterRequest register = new(email, "StrongPass1!", "StrongPass1!", "Moto Rider", "+52 555 555 5555", accountType == "Admin" ? "Rider" : accountType, true);
        await client.PostAsJsonAsync("/api/v1/auth/register", register);
        User user = stores.Users.Users.Single(user => string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase));
        if (accountType == "Admin") user.Role = UserRole.Admin;
        LoginEnvelope login = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, register.Password))).Content.ReadFromJsonAsync<LoginEnvelope>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Data.AccessToken);
        return user;
    }

    private static WebApplicationFactory<Program> CreateFactory(TestStores stores) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "MotoSOS",
            ["Jwt:Audience"] = "MotoSOS.Clients",
            ["Jwt:Key"] = new string('D', 48),
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
            services.AddSingleton<IDeviceActivationCodeRepository>(stores.Codes);
            services.AddSingleton<IUserDeviceRepository>(stores.Devices);
            services.AddSingleton<IUserSubscriptionRepository>(stores.Subscriptions);
        });
    });

    private sealed class TestStores { public InMemoryUserRepository Users { get; } = new(); public InMemoryRefreshTokenRepository RefreshTokens { get; } = new(); public InMemoryDriverProfileRepository Profiles { get; } = new(); public InMemoryDriverVehicleRepository Vehicles { get; } = new(); public InMemoryEmergencyContactRepository Contacts { get; } = new(); public InMemoryActivationCodeRepository Codes { get; } = new(); public InMemoryUserDeviceRepository Devices { get; } = new(); public InMemoryUserSubscriptionRepository Subscriptions { get; } = new(); }
    private sealed class InMemoryUserRepository : IUserRepository { public List<User> Users { get; } = []; public Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(Users.FirstOrDefault(user => user.Id == id)); public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) => Task.FromResult(Users.FirstOrDefault(user => string.Equals(user.Email, email.Trim(), StringComparison.OrdinalIgnoreCase))); public Task AddAsync(User user, CancellationToken cancellationToken) { Users.Add(user); return Task.CompletedTask; } public Task UpdateAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryRefreshTokenRepository : IRefreshTokenRepository { public List<RefreshToken> Tokens { get; } = []; public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) => Task.FromResult(Tokens.FirstOrDefault(token => token.TokenHash == tokenHash)); public Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken) { Tokens.Add(refreshToken); return Task.CompletedTask; } public Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryDriverProfileRepository : IDriverProfileRepository { public List<DriverProfile> Profiles { get; } = []; public Task<DriverProfile?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Profiles.FirstOrDefault(profile => profile.UserId == userId)); public Task AddAsync(DriverProfile profile, CancellationToken cancellationToken) { Profiles.Add(profile); return Task.CompletedTask; } public Task UpdateAsync(DriverProfile profile, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryDriverVehicleRepository : IDriverVehicleRepository { public List<DriverVehicle> Vehicles { get; } = []; public Task<IReadOnlyList<DriverVehicle>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DriverVehicle>>(Vehicles.Where(vehicle => vehicle.UserId == userId && vehicle.IsActive).ToArray()); public Task<DriverVehicle?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(Vehicles.FirstOrDefault(vehicle => vehicle.Id == id)); public Task<int> CountActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Vehicles.Count(vehicle => vehicle.UserId == userId && vehicle.IsActive)); public Task AddAsync(DriverVehicle vehicle, CancellationToken cancellationToken) { Vehicles.Add(vehicle); return Task.CompletedTask; } public Task UpdateAsync(DriverVehicle vehicle, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryEmergencyContactRepository : IEmergencyContactRepository { public List<EmergencyContact> Contacts { get; } = []; public Task<IReadOnlyList<EmergencyContact>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<EmergencyContact>>(Contacts.Where(contact => contact.UserId == userId && contact.IsActive).ToArray()); public Task<EmergencyContact?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(Contacts.FirstOrDefault(contact => contact.Id == id)); public Task<EmergencyContact?> GetByLinkingCodeAsync(string linkingCode, CancellationToken cancellationToken) => Task.FromResult(Contacts.FirstOrDefault(contact => contact.LinkingCode == linkingCode)); public Task<int> CountActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Contacts.Count(contact => contact.UserId == userId && contact.IsActive)); public Task AddAsync(EmergencyContact contact, CancellationToken cancellationToken) { Contacts.Add(contact); return Task.CompletedTask; } public Task UpdateAsync(EmergencyContact contact, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryActivationCodeRepository : IDeviceActivationCodeRepository { public List<DeviceActivationCode> Codes { get; } = []; public Task<IReadOnlyList<DeviceActivationCode>> GetActiveByUserIdAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DeviceActivationCode>>(Codes.Where(code => code.UserId == userId && !code.IsUsed && !code.IsRevoked && code.ExpiresAtUtc > now).ToArray()); public Task<DeviceActivationCode?> GetActiveCurrentByUserIdAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult(Codes.LastOrDefault(code => code.UserId == userId && !code.IsUsed && !code.IsRevoked && code.ExpiresAtUtc > now)); public Task<DeviceActivationCode?> GetByCodeAsync(string code, CancellationToken cancellationToken) => Task.FromResult(Codes.FirstOrDefault(activationCode => activationCode.Code == code)); public Task AddAsync(DeviceActivationCode code, CancellationToken cancellationToken) { Codes.Add(code); return Task.CompletedTask; } public Task UpdateAsync(DeviceActivationCode code, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryUserDeviceRepository : IUserDeviceRepository { public List<UserDevice> Devices { get; } = []; public Task<IReadOnlyList<UserDevice>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<UserDevice>>(Devices.Where(device => device.UserId == userId && device.IsActive).ToArray()); public Task<IReadOnlyList<UserDevice>> GetActiveByParentDeviceIdAsync(string parentDeviceId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<UserDevice>>(Devices.Where(device => device.ParentDeviceId == parentDeviceId && device.IsActive).ToArray()); public Task<UserDevice?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(Devices.FirstOrDefault(device => device.Id == id)); public Task<UserDevice?> GetByDeviceIdentifierHashAsync(string userId, string hash, DeviceType deviceType, CancellationToken cancellationToken) => Task.FromResult(Devices.FirstOrDefault(device => device.UserId == userId && device.DeviceIdentifierHash == hash && device.DeviceType == deviceType)); public Task<int> CountActiveLinkedByUserIdAndTypeAsync(string userId, DeviceType deviceType, CancellationToken cancellationToken) => Task.FromResult(Devices.Count(device => device.UserId == userId && device.DeviceType == deviceType && device.IsActive && device.LinkStatus == DeviceLinkStatus.Linked)); public Task<bool> HasActiveLinkedMobileAppAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Devices.Any(device => device.UserId == userId && device.DeviceType == DeviceType.MobileApp && device.IsActive && device.LinkStatus == DeviceLinkStatus.Linked)); public Task AddAsync(UserDevice device, CancellationToken cancellationToken) { Devices.Add(device); return Task.CompletedTask; } public Task UpdateAsync(UserDevice device, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryUserSubscriptionRepository : IUserSubscriptionRepository { public List<UserSubscription> Subscriptions { get; } = []; public Task<UserSubscription?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Subscriptions.FirstOrDefault(subscription => subscription.UserId == userId)); public Task<bool> HasActiveSubscriptionAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Subscriptions.Any(subscription => subscription.UserId == userId && subscription.Status == SubscriptionStatus.Active)); public Task AddAsync(UserSubscription subscription, CancellationToken cancellationToken) { Subscriptions.Add(subscription); return Task.CompletedTask; } public Task UpdateAsync(UserSubscription subscription, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed record LoginEnvelope(bool Success, LoginResponse Data);
    private sealed record CodeEnvelope(bool Success, CreateMobileActivationCodeResponse Data);
    private sealed record LinkEnvelope(bool Success, LinkMobileDeviceResponse Data);
    private sealed record WatchEnvelope(bool Success, LinkSmartwatchResponse Data);
}
