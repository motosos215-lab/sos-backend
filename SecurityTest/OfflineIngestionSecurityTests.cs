using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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
using MotoSOS.API.Modules.OfflineIngestion.Application;
using MotoSOS.API.Modules.OfflineIngestion.Contracts;
using MotoSOS.API.Modules.OfflineIngestion.Domain;
using MotoSOS.API.Modules.Onboarding.Application;
using MotoSOS.API.Modules.Onboarding.Domain;
using MotoSOS.API.Modules.Plans.Application;
using MotoSOS.API.Modules.Plans.Domain;
using MotoSOS.API.Modules.Profiles.Application;
using MotoSOS.API.Modules.Profiles.Domain;
using MotoSOS.API.Modules.Trips.Application;
using MotoSOS.API.Modules.Trips.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;
using MotoSOS.API.Modules.Vehicles.Application;
using MotoSOS.API.Modules.Vehicles.Domain;

namespace SecurityTest;

public sealed class OfflineIngestionSecurityTests
{
    [Fact]
    public async Task OfflineIngestionResponseDoesNotExposeSensitiveDataOrPayload()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        User user = await AuthenticateAsync(client, "offline-security@example.com", "Rider", stores);
        var mobile = new UserDevice { UserId = user.Id, DeviceType = DeviceType.MobileApp, DeviceName = "Phone", IsActive = true, LinkStatus = DeviceLinkStatus.Linked, DeviceIdentifierHash = "hashed-device-id" };
        stores.Devices.Devices.Add(mobile);
        var trip = new Trip { UserId = user.Id, MobileDeviceId = mobile.Id, VehicleId = "vehicle", Status = TripStatus.Active, StartedAtUtc = DateTimeOffset.UtcNow, CreatedAtUtc = DateTimeOffset.UtcNow };
        stores.Trips.Trips.Add(trip);

        string body = await (await client.PostAsJsonAsync("/api/v1/mobile/offline-ingestion/batch", Batch(mobile.Id, trip.Id))).Content.ReadAsStringAsync();

        body.Should().Contain("ackId");
        body.Should().Contain("remoteRecordId");
        body.Should().NotContain("passwordHash");
        body.Should().NotContain("refreshToken");
        body.Should().NotContain("accessToken");
        body.Should().NotContain("deviceIdentifier");
        body.Should().NotContain("DeviceIdentifierHash");
        body.Should().NotContain("hashed-device-id");
        body.Should().NotContain("score");
        body.Should().NotContain("GooglePlay");
        body.Should().NotContain("Stripe");
        body.Should().NotContain("Payment");
    }

    [Theory]
    [InlineData("Monitor")]
    [InlineData("Admin")]
    public async Task MonitorAndAdminCannotUseOfflineIngestion(string role)
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        await AuthenticateAsync(client, $"offline-security-{role}@example.com", role, stores);

        (await client.PostAsJsonAsync("/api/v1/mobile/offline-ingestion/batch", Batch("m", "t"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static OfflineIngestionBatchRequest Batch(string mobileId, string tripId) => new(Guid.NewGuid().ToString(), mobileId, tripId, 1, DateTimeOffset.UtcNow, "1.0.0", [new OfflineIngestionItemRequest(Guid.NewGuid().ToString(), "minor-event", DateTimeOffset.UtcNow, 1, JsonDocument.Parse("{\"score\":35}").RootElement.Clone())]);
    private static async Task<User> AuthenticateAsync(HttpClient client, string email, string accountType, TestStores stores) { RegisterRequest register = new(email, "StrongPass1!", "StrongPass1!", "Moto Rider", "+52 555 555 5555", "Rider", true); await client.PostAsJsonAsync("/api/v1/auth/register", register); User user = stores.Users.Users.Single(user => string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)); if (accountType == "Admin") user.Role = UserRole.Admin; if (accountType == "Monitor") user.Role = UserRole.Monitor; LoginEnvelope login = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, register.Password))).Content.ReadFromJsonAsync<LoginEnvelope>())!; client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Data.AccessToken); return user; }
    private static WebApplicationFactory<Program> CreateFactory(TestStores stores) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Issuer"] = "MotoSOS", ["Jwt:Audience"] = "MotoSOS.Clients", ["Jwt:Key"] = new string('Y', 48), ["Jwt:AccessTokenMinutes"] = "15", ["Jwt:RefreshTokenDays"] = "7", ["Jwt:RefreshTokenRememberMeDays"] = "30", ["MongoDb:ConnectionString"] = string.Empty, ["MongoDb:DatabaseName"] = "MotoSOS_Test" })); builder.ConfigureTestServices(services => { services.AddSingleton<IUserRepository>(stores.Users); services.AddSingleton<IRefreshTokenRepository>(stores.RefreshTokens); services.AddSingleton<IDriverProfileRepository>(stores.Profiles); services.AddSingleton<IDriverVehicleRepository>(stores.Vehicles); services.AddSingleton<IEmergencyContactRepository>(stores.Contacts); services.AddSingleton<IDeviceActivationCodeRepository>(stores.Codes); services.AddSingleton<IUserDeviceRepository>(stores.Devices); services.AddSingleton<IUserSubscriptionRepository>(stores.Subscriptions); services.AddSingleton<IOnboardingConfirmationRepository>(stores.Confirmations); services.AddSingleton<ITripRepository>(stores.Trips); services.AddSingleton<IOfflineIngestionRepository>(stores.OfflineRecords); }); });
    private sealed class TestStores { public InMemoryUserRepository Users { get; } = new(); public InMemoryRefreshTokenRepository RefreshTokens { get; } = new(); public EmptyProfiles Profiles { get; } = new(); public EmptyVehicles Vehicles { get; } = new(); public EmptyContacts Contacts { get; } = new(); public EmptyCodes Codes { get; } = new(); public InMemoryDevices Devices { get; } = new(); public EmptySubscriptions Subscriptions { get; } = new(); public EmptyConfirmations Confirmations { get; } = new(); public InMemoryTrips Trips { get; } = new(); public InMemoryOffline OfflineRecords { get; } = new(); }
    private sealed class InMemoryUserRepository : IUserRepository { public List<User> Users { get; } = []; public Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(Users.FirstOrDefault(user => user.Id == id)); public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) => Task.FromResult(Users.FirstOrDefault(user => user.Email == email)); public Task AddAsync(User user, CancellationToken cancellationToken) { Users.Add(user); return Task.CompletedTask; } public Task UpdateAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryRefreshTokenRepository : IRefreshTokenRepository { public List<RefreshToken> Tokens { get; } = []; public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) => Task.FromResult(Tokens.FirstOrDefault(token => token.TokenHash == tokenHash)); public Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken) { Tokens.Add(refreshToken); return Task.CompletedTask; } public Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryDevices : IUserDeviceRepository { public List<UserDevice> Devices { get; } = []; public Task<IReadOnlyList<UserDevice>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<UserDevice>>([]); public Task<IReadOnlyList<UserDevice>> GetActiveByParentDeviceIdAsync(string parentDeviceId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<UserDevice>>([]); public Task<UserDevice?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(Devices.FirstOrDefault(device => device.Id == id)); public Task<UserDevice?> GetByDeviceIdentifierHashAsync(string userId, string hash, DeviceType deviceType, CancellationToken cancellationToken) => Task.FromResult<UserDevice?>(null); public Task<int> CountActiveLinkedByUserIdAndTypeAsync(string userId, DeviceType deviceType, CancellationToken cancellationToken) => Task.FromResult(0); public Task<bool> HasActiveLinkedMobileAppAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(false); public Task AddAsync(UserDevice device, CancellationToken cancellationToken) => Task.CompletedTask; public Task UpdateAsync(UserDevice device, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryTrips : ITripRepository { public List<Trip> Trips { get; } = []; public Task<Trip?> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Trips.FirstOrDefault()); public Task<Trip?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(Trips.FirstOrDefault(trip => trip.Id == id)); public Task<IReadOnlyList<Trip>> ListByUserIdAsync(string userId, TripStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Trip>>([]); public Task<long> CountByUserIdAsync(string userId, TripStatus? status, CancellationToken cancellationToken) => Task.FromResult(0L); public Task AddAsync(Trip trip, CancellationToken cancellationToken) => Task.CompletedTask; public Task UpdateAsync(Trip trip, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryOffline : IOfflineIngestionRepository { public List<OfflineIngestionRecord> Records { get; } = []; public Task<OfflineIngestionRecord?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) => Task.FromResult(Records.FirstOrDefault(record => record.IdempotencyKey == idempotencyKey)); public Task<(OfflineIngestionRecord Record, bool IsDuplicate)> AddOrGetDuplicateAsync(OfflineIngestionRecord record, CancellationToken cancellationToken) { Records.Add(record); return Task.FromResult((record, false)); } }
    private sealed class EmptyProfiles : IDriverProfileRepository { public Task<DriverProfile?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<DriverProfile?>(null); public Task AddAsync(DriverProfile profile, CancellationToken cancellationToken) => Task.CompletedTask; public Task UpdateAsync(DriverProfile profile, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class EmptyVehicles : IDriverVehicleRepository { public Task<IReadOnlyList<DriverVehicle>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DriverVehicle>>([]); public Task<DriverVehicle?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult<DriverVehicle?>(null); public Task<int> CountActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(0); public Task AddAsync(DriverVehicle vehicle, CancellationToken cancellationToken) => Task.CompletedTask; public Task UpdateAsync(DriverVehicle vehicle, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class EmptyContacts : IEmergencyContactRepository { public Task<IReadOnlyList<EmergencyContact>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<EmergencyContact>>([]); public Task<EmergencyContact?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult<EmergencyContact?>(null); public Task<EmergencyContact?> GetByLinkingCodeAsync(string linkingCode, CancellationToken cancellationToken) => Task.FromResult<EmergencyContact?>(null); public Task<int> CountActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(0); public Task AddAsync(EmergencyContact contact, CancellationToken cancellationToken) => Task.CompletedTask; public Task UpdateAsync(EmergencyContact contact, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class EmptyCodes : IDeviceActivationCodeRepository { public Task<IReadOnlyList<DeviceActivationCode>> GetActiveByUserIdAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DeviceActivationCode>>([]); public Task<DeviceActivationCode?> GetActiveCurrentByUserIdAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult<DeviceActivationCode?>(null); public Task<DeviceActivationCode?> GetByCodeAsync(string code, CancellationToken cancellationToken) => Task.FromResult<DeviceActivationCode?>(null); public Task AddAsync(DeviceActivationCode code, CancellationToken cancellationToken) => Task.CompletedTask; public Task UpdateAsync(DeviceActivationCode code, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class EmptySubscriptions : IUserSubscriptionRepository { public Task<UserSubscription?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<UserSubscription?>(null); public Task<bool> HasActiveSubscriptionAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(false); public Task AddAsync(UserSubscription subscription, CancellationToken cancellationToken) => Task.CompletedTask; public Task UpdateAsync(UserSubscription subscription, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class EmptyConfirmations : IOnboardingConfirmationRepository { public Task<OnboardingConfirmation?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<OnboardingConfirmation?>(null); public Task AddAsync(OnboardingConfirmation confirmation, CancellationToken cancellationToken) => Task.CompletedTask; public Task UpdateAsync(OnboardingConfirmation confirmation, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed record LoginEnvelope(bool Success, LoginResponse Data);
}
