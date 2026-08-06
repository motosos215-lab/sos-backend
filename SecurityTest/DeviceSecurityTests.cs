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

namespace SecurityTest;

public sealed class DeviceSecurityTests
{
    [Fact]
    public async Task DeviceResponsesDoNotExposeSecretsOrPlainDeviceIdentifier()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        await AuthenticateAsync(client, "devices-safe@example.com", stores);
        string code = (await (await client.PostAsync("/api/v1/devices/mobile/activation-code", null)).Content.ReadFromJsonAsync<CodeEnvelope>())!.Data.ActivationCode!.Code;
        await client.PostAsJsonAsync("/api/v1/devices/mobile/link", MobileRequest(code));

        string body = await (await client.GetAsync("/api/v1/devices")).Content.ReadAsStringAsync();

        body.Should().NotContain("passwordHash");
        body.Should().NotContain("refreshToken");
        body.Should().NotContain("deviceIdentifier");
        body.Should().NotContain("local-device-id-from-mobile");
        body.Should().NotContain("deviceIdentifierHash");
    }

    [Fact]
    public async Task UnexpectedFieldsDoNotChangeUserSecurityFields()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        User user = await AuthenticateAsync(client, "devices-immutable@example.com", stores);
        string code = (await (await client.PostAsync("/api/v1/devices/mobile/activation-code", null)).Content.ReadFromJsonAsync<CodeEnvelope>())!.Data.ActivationCode!.Code;

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/devices/mobile/link", new
        {
            code,
            deviceName = "Motorola Edge",
            platform = "Android",
            deviceIdentifier = "local-device-id-from-mobile",
            email = "attacker@example.com",
            role = "Admin",
            isActive = false,
            permissions = "admin"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        user.Email.Should().Be("devices-immutable@example.com");
        user.Role.Should().Be(UserRole.Rider);
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CannotAccessOtherUsersDeviceAndRevokeIsLogical()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient first = factory.CreateClient();
        HttpClient second = factory.CreateClient();
        User owner = await AuthenticateAsync(first, "devices-owner-security@example.com", stores);
        var device = new UserDevice { UserId = owner.Id, DeviceType = DeviceType.MobileApp, DeviceName = "Phone", IsActive = true, LinkStatus = DeviceLinkStatus.Linked };
        stores.Devices.Devices.Add(device);
        await AuthenticateAsync(second, "devices-other-security@example.com", stores);

        HttpResponseMessage heartbeat = await second.PatchAsJsonAsync($"/api/v1/devices/{device.Id}/heartbeat", new HeartbeatDeviceRequest(80, "Online", null));
        HttpResponseMessage revoke = await first.PostAsync($"/api/v1/devices/{device.Id}/revoke", null);

        heartbeat.StatusCode.Should().Be(HttpStatusCode.NotFound);
        revoke.StatusCode.Should().Be(HttpStatusCode.NoContent);
        stores.Devices.Devices.Should().ContainSingle(existing => existing.Id == device.Id && !existing.IsActive && existing.LinkStatus == DeviceLinkStatus.Revoked);
    }

    [Fact]
    public async Task ActivationCodeHasNoSensitiveData()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        await AuthenticateAsync(client, "devices-code-security@example.com", stores);

        CodeEnvelope response = (await (await client.PostAsync("/api/v1/devices/mobile/activation-code", null)).Content.ReadFromJsonAsync<CodeEnvelope>())!;

        response.Data.ActivationCode!.Code.Should().StartWith("MSOS-");
        response.Data.ActivationCode.Code.Should().NotContain("devices-code-security@example.com");
        response.Data.ActivationCode.Code.Should().NotContain("Moto");
    }

    private static LinkMobileDeviceRequest MobileRequest(string code) => new(code, "Motorola Edge", "Android", "Motorola", "Edge 40", "14", "1.0.0", "local-device-id-from-mobile");

    private static async Task<User> AuthenticateAsync(HttpClient client, string email, TestStores stores)
    {
        RegisterRequest register = new(email, "StrongPass1!", "StrongPass1!", "Moto Rider", "+52 555 555 5555", "Rider", true);
        await client.PostAsJsonAsync("/api/v1/auth/register", register);
        LoginEnvelope login = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, register.Password))).Content.ReadFromJsonAsync<LoginEnvelope>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Data.AccessToken);
        return stores.Users.Users.Single(user => user.Email == email);
    }

    private static WebApplicationFactory<Program> CreateFactory(TestStores stores) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "MotoSOS",
            ["Jwt:Audience"] = "MotoSOS.Clients",
            ["Jwt:Key"] = new string('S', 48),
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
    private sealed class InMemoryDriverProfileRepository : IDriverProfileRepository { public Task<DriverProfile?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<DriverProfile?>(null); public Task AddAsync(DriverProfile profile, CancellationToken cancellationToken) => Task.CompletedTask; public Task UpdateAsync(DriverProfile profile, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryDriverVehicleRepository : IDriverVehicleRepository { public Task<IReadOnlyList<DriverVehicle>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DriverVehicle>>([]); public Task<DriverVehicle?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult<DriverVehicle?>(null); public Task<int> CountActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(0); public Task AddAsync(DriverVehicle vehicle, CancellationToken cancellationToken) => Task.CompletedTask; public Task UpdateAsync(DriverVehicle vehicle, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryEmergencyContactRepository : IEmergencyContactRepository { public Task<IReadOnlyList<EmergencyContact>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<EmergencyContact>>([]); public Task<EmergencyContact?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult<EmergencyContact?>(null); public Task<EmergencyContact?> GetByLinkingCodeAsync(string linkingCode, CancellationToken cancellationToken) => Task.FromResult<EmergencyContact?>(null); public Task<int> CountActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(0); public Task AddAsync(EmergencyContact contact, CancellationToken cancellationToken) => Task.CompletedTask; public Task UpdateAsync(EmergencyContact contact, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryActivationCodeRepository : IDeviceActivationCodeRepository { public List<DeviceActivationCode> Codes { get; } = []; public Task<IReadOnlyList<DeviceActivationCode>> GetActiveByUserIdAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DeviceActivationCode>>(Codes.Where(code => code.UserId == userId && !code.IsUsed && !code.IsRevoked && code.ExpiresAtUtc > now).ToArray()); public Task<DeviceActivationCode?> GetActiveCurrentByUserIdAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult(Codes.LastOrDefault(code => code.UserId == userId && !code.IsUsed && !code.IsRevoked && code.ExpiresAtUtc > now)); public Task<DeviceActivationCode?> GetByCodeAsync(string code, CancellationToken cancellationToken) => Task.FromResult(Codes.FirstOrDefault(activationCode => activationCode.Code == code)); public Task AddAsync(DeviceActivationCode code, CancellationToken cancellationToken) { Codes.Add(code); return Task.CompletedTask; } public Task UpdateAsync(DeviceActivationCode code, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryUserDeviceRepository : IUserDeviceRepository { public List<UserDevice> Devices { get; } = []; public Task<IReadOnlyList<UserDevice>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<UserDevice>>(Devices.Where(device => device.UserId == userId && device.IsActive).ToArray()); public Task<IReadOnlyList<UserDevice>> GetActiveByParentDeviceIdAsync(string parentDeviceId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<UserDevice>>(Devices.Where(device => device.ParentDeviceId == parentDeviceId && device.IsActive).ToArray()); public Task<UserDevice?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(Devices.FirstOrDefault(device => device.Id == id)); public Task<UserDevice?> GetByDeviceIdentifierHashAsync(string userId, string hash, DeviceType deviceType, CancellationToken cancellationToken) => Task.FromResult(Devices.FirstOrDefault(device => device.UserId == userId && device.DeviceIdentifierHash == hash && device.DeviceType == deviceType)); public Task<int> CountActiveLinkedByUserIdAndTypeAsync(string userId, DeviceType deviceType, CancellationToken cancellationToken) => Task.FromResult(Devices.Count(device => device.UserId == userId && device.DeviceType == deviceType && device.IsActive && device.LinkStatus == DeviceLinkStatus.Linked)); public Task<bool> HasActiveLinkedMobileAppAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Devices.Any(device => device.UserId == userId && device.DeviceType == DeviceType.MobileApp && device.IsActive && device.LinkStatus == DeviceLinkStatus.Linked)); public Task AddAsync(UserDevice device, CancellationToken cancellationToken) { Devices.Add(device); return Task.CompletedTask; } public Task UpdateAsync(UserDevice device, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryUserSubscriptionRepository : IUserSubscriptionRepository { public List<UserSubscription> Subscriptions { get; } = []; public Task<UserSubscription?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Subscriptions.FirstOrDefault(subscription => subscription.UserId == userId)); public Task<bool> HasActiveSubscriptionAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Subscriptions.Any(subscription => subscription.UserId == userId && subscription.Status == SubscriptionStatus.Active)); public Task AddAsync(UserSubscription subscription, CancellationToken cancellationToken) { Subscriptions.Add(subscription); return Task.CompletedTask; } public Task UpdateAsync(UserSubscription subscription, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed record LoginEnvelope(bool Success, LoginResponse Data);
    private sealed record CodeEnvelope(bool Success, CreateMobileActivationCodeResponse Data);
}
