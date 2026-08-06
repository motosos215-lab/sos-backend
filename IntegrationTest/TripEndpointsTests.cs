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
using MotoSOS.API.Modules.Trips.Application;
using MotoSOS.API.Modules.Trips.Contracts;
using MotoSOS.API.Modules.Trips.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;
using MotoSOS.API.Modules.Vehicles.Application;
using MotoSOS.API.Modules.Vehicles.Domain;

namespace IntegrationTest;

public sealed class TripEndpointsTests
{
    [Fact]
    public async Task TripsRequireAuthentication()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory(new TestStores());
        HttpClient client = factory.CreateClient();

        (await client.GetAsync("/api/v1/trips/active")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/v1/trips/start", StartRequest("v", "m"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("Monitor")]
    [InlineData("Admin")]
    public async Task NonRidersReceiveForbidden(string role)
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        await AuthenticateAsync(client, $"trips-{role}@example.com", role, stores);

        (await client.GetAsync("/api/v1/trips/active")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RiderWithCompletedOnboardingCanStartGetListAndFinishTrip()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        User user = await AuthenticateAsync(client, "trips-ready@example.com", "Rider", stores);
        (DriverVehicle vehicle, UserDevice mobile) = SeedReadyState(stores, user.Id);

        HttpResponseMessage start = await client.PostAsJsonAsync("/api/v1/trips/start", StartRequest(vehicle.Id, mobile.Id));
        TripEnvelope started = (await start.Content.ReadFromJsonAsync<TripEnvelope>())!;
        HttpResponseMessage active = await client.GetAsync("/api/v1/trips/active");
        HttpResponseMessage get = await client.GetAsync($"/api/v1/trips/{started.Data.Trip.Id}");
        string list = await (await client.GetAsync("/api/v1/trips")).Content.ReadAsStringAsync();
        HttpResponseMessage finish = await client.PostAsJsonAsync($"/api/v1/trips/{started.Data.Trip.Id}/finish", FinishRequest());
        HttpResponseMessage secondFinish = await client.PostAsJsonAsync($"/api/v1/trips/{started.Data.Trip.Id}/finish", FinishRequest());

        start.StatusCode.Should().Be(HttpStatusCode.OK);
        started.Data.Trip.Status.Should().Be("Active");
        active.StatusCode.Should().Be(HttpStatusCode.OK);
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        list.Should().Contain(started.Data.Trip.Id);
        finish.StatusCode.Should().Be(HttpStatusCode.OK);
        secondFinish.StatusCode.Should().Be(HttpStatusCode.OK);
        stores.Trips.Trips.Single(trip => trip.Id == started.Data.Trip.Id).Status.Should().Be(TripStatus.Finished);
    }

    [Fact]
    public async Task IncompleteOnboardingCannotStartTrip()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        User user = await AuthenticateAsync(client, "trips-incomplete@example.com", "Rider", stores);
        var vehicle = new DriverVehicle { UserId = user.Id, IsActive = true, CompletionStatus = VehicleCompletionStatus.Completed };
        var mobile = new UserDevice { UserId = user.Id, DeviceType = DeviceType.MobileApp, DeviceName = "Phone", IsActive = true, LinkStatus = DeviceLinkStatus.Linked };
        stores.Vehicles.Vehicles.Add(vehicle);
        stores.Devices.Devices.Add(mobile);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/trips/start", StartRequest(vehicle.Id, mobile.Id));
        string body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain("onboarding_not_ready");
    }

    [Fact]
    public async Task RiderCannotStartWithOtherUsersVehicleOrMobileDevice()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        User user = await AuthenticateAsync(client, "trips-owner@example.com", "Rider", stores);
        User other = await AuthenticateAsync(factory.CreateClient(), "trips-other@example.com", "Rider", stores);
        (DriverVehicle vehicle, UserDevice mobile) = SeedReadyState(stores, user.Id);
        var otherVehicle = new DriverVehicle { UserId = other.Id, IsActive = true, CompletionStatus = VehicleCompletionStatus.Completed };
        var otherMobile = new UserDevice { UserId = other.Id, DeviceType = DeviceType.MobileApp, DeviceName = "Other", IsActive = true, LinkStatus = DeviceLinkStatus.Linked };
        stores.Vehicles.Vehicles.Add(otherVehicle);
        stores.Devices.Devices.Add(otherMobile);

        HttpResponseMessage otherVehicleResponse = await client.PostAsJsonAsync("/api/v1/trips/start", StartRequest(otherVehicle.Id, mobile.Id));
        HttpResponseMessage otherMobileResponse = await client.PostAsJsonAsync("/api/v1/trips/start", StartRequest(vehicle.Id, otherMobile.Id));

        otherVehicleResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        otherMobileResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ActiveTripRulesAreIdempotentForSameStartAndConflictForDifferentStart()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        User user = await AuthenticateAsync(client, "trips-active@example.com", "Rider", stores);
        (DriverVehicle vehicle, UserDevice mobile) = SeedReadyState(stores, user.Id);
        var otherVehicle = new DriverVehicle { UserId = user.Id, IsActive = true, CompletionStatus = VehicleCompletionStatus.Completed };
        stores.Vehicles.Vehicles.Add(otherVehicle);

        TripEnvelope first = (await (await client.PostAsJsonAsync("/api/v1/trips/start", StartRequest(vehicle.Id, mobile.Id))).Content.ReadFromJsonAsync<TripEnvelope>())!;
        TripEnvelope second = (await (await client.PostAsJsonAsync("/api/v1/trips/start", StartRequest(vehicle.Id, mobile.Id))).Content.ReadFromJsonAsync<TripEnvelope>())!;
        HttpResponseMessage conflict = await client.PostAsJsonAsync("/api/v1/trips/start", StartRequest(otherVehicle.Id, mobile.Id));

        second.Data.Trip.Id.Should().Be(first.Data.Trip.Id);
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await conflict.Content.ReadAsStringAsync()).Should().Contain("active_trip_exists");
    }

    [Fact]
    public async Task TripsAreScopedToAuthenticatedUser()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient owner = factory.CreateClient();
        HttpClient otherClient = factory.CreateClient();
        User ownerUser = await AuthenticateAsync(owner, "trips-scope-owner@example.com", "Rider", stores);
        await AuthenticateAsync(otherClient, "trips-scope-other@example.com", "Rider", stores);
        var trip = new Trip { UserId = ownerUser.Id, VehicleId = "v", MobileDeviceId = "m", Status = TripStatus.Active, StartedAtUtc = DateTimeOffset.UtcNow, CreatedAtUtc = DateTimeOffset.UtcNow };
        stores.Trips.Trips.Add(trip);

        (await otherClient.GetAsync($"/api/v1/trips/{trip.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await otherClient.PostAsJsonAsync($"/api/v1/trips/{trip.Id}/finish", FinishRequest())).StatusCode.Should().Be(HttpStatusCode.NotFound);
        string list = await (await otherClient.GetAsync("/api/v1/trips")).Content.ReadAsStringAsync();
        list.Should().NotContain(trip.Id);
    }

    private static (DriverVehicle Vehicle, UserDevice Mobile) SeedReadyState(TestStores stores, string userId)
    {
        stores.Profiles.Profiles.Add(new DriverProfile { UserId = userId, CompletionStatus = ProfileCompletionStatus.Completed });
        var vehicle = new DriverVehicle { UserId = userId, IsActive = true, CompletionStatus = VehicleCompletionStatus.Completed, VehicleType = VehicleType.Motorcycle, Brand = "Yamaha", Model = "FZ", Year = 2024, Alias = "Moto" };
        stores.Vehicles.Vehicles.Add(vehicle);
        stores.Contacts.Contacts.Add(new EmergencyContact { UserId = userId, IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Invited });
        var mobile = new UserDevice { UserId = userId, DeviceType = DeviceType.MobileApp, DeviceName = "Phone", IsActive = true, LinkStatus = DeviceLinkStatus.Linked };
        stores.Devices.Devices.Add(mobile);
        stores.Subscriptions.Subscriptions.Add(new UserSubscription { UserId = userId, PlanTier = PlanTier.Basic, Status = SubscriptionStatus.Active, Source = SubscriptionSource.WebBasic });
        stores.Confirmations.Confirmations.Add(new OnboardingConfirmation { UserId = userId, IsOperational = true, ConfirmedAtUtc = DateTimeOffset.UtcNow });
        return (vehicle, mobile);
    }

    private static StartTripRequest StartRequest(string vehicleId, string mobileId) => new(vehicleId, mobileId, null, DateTimeOffset.UtcNow, new TripLocationRequest(19.2826, -99.6557, 12.5, "gps", DateTimeOffset.UtcNow), 87, "1.0.0");
    private static FinishTripRequest FinishRequest() => new(DateTimeOffset.UtcNow, new TripLocationRequest(19.2850, -99.6600, 10, "gps", DateTimeOffset.UtcNow), 75, "Viaje finalizado");

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
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Issuer"] = "MotoSOS", ["Jwt:Audience"] = "MotoSOS.Clients", ["Jwt:Key"] = new string('T', 48), ["Jwt:AccessTokenMinutes"] = "15", ["Jwt:RefreshTokenDays"] = "7", ["Jwt:RefreshTokenRememberMeDays"] = "30", ["MongoDb:ConnectionString"] = string.Empty, ["MongoDb:DatabaseName"] = "MotoSOS_Test" }));
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
            services.AddSingleton<IOnboardingConfirmationRepository>(stores.Confirmations);
            services.AddSingleton<ITripRepository>(stores.Trips);
        });
    });

    private sealed class TestStores { public InMemoryUserRepository Users { get; } = new(); public InMemoryRefreshTokenRepository RefreshTokens { get; } = new(); public InMemoryDriverProfileRepository Profiles { get; } = new(); public InMemoryDriverVehicleRepository Vehicles { get; } = new(); public InMemoryEmergencyContactRepository Contacts { get; } = new(); public InMemoryActivationCodeRepository Codes { get; } = new(); public InMemoryUserDeviceRepository Devices { get; } = new(); public InMemoryUserSubscriptionRepository Subscriptions { get; } = new(); public InMemoryOnboardingConfirmationRepository Confirmations { get; } = new(); public InMemoryTripRepository Trips { get; } = new(); }
    private sealed class InMemoryUserRepository : IUserRepository { public List<User> Users { get; } = []; public Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(Users.FirstOrDefault(user => user.Id == id)); public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) => Task.FromResult(Users.FirstOrDefault(user => string.Equals(user.Email, email.Trim(), StringComparison.OrdinalIgnoreCase))); public Task AddAsync(User user, CancellationToken cancellationToken) { Users.Add(user); return Task.CompletedTask; } public Task UpdateAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryRefreshTokenRepository : IRefreshTokenRepository { public List<RefreshToken> Tokens { get; } = []; public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) => Task.FromResult(Tokens.FirstOrDefault(token => token.TokenHash == tokenHash)); public Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken) { Tokens.Add(refreshToken); return Task.CompletedTask; } public Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryDriverProfileRepository : IDriverProfileRepository { public List<DriverProfile> Profiles { get; } = []; public Task<DriverProfile?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Profiles.FirstOrDefault(profile => profile.UserId == userId)); public Task AddAsync(DriverProfile profile, CancellationToken cancellationToken) { Profiles.Add(profile); return Task.CompletedTask; } public Task UpdateAsync(DriverProfile profile, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryDriverVehicleRepository : IDriverVehicleRepository { public List<DriverVehicle> Vehicles { get; } = []; public Task<IReadOnlyList<DriverVehicle>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DriverVehicle>>(Vehicles.Where(vehicle => vehicle.UserId == userId && vehicle.IsActive).ToArray()); public Task<DriverVehicle?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(Vehicles.FirstOrDefault(vehicle => vehicle.Id == id)); public Task<int> CountActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Vehicles.Count(vehicle => vehicle.UserId == userId && vehicle.IsActive)); public Task AddAsync(DriverVehicle vehicle, CancellationToken cancellationToken) { Vehicles.Add(vehicle); return Task.CompletedTask; } public Task UpdateAsync(DriverVehicle vehicle, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryEmergencyContactRepository : IEmergencyContactRepository { public List<EmergencyContact> Contacts { get; } = []; public Task<IReadOnlyList<EmergencyContact>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<EmergencyContact>>(Contacts.Where(contact => contact.UserId == userId && contact.IsActive).ToArray()); public Task<EmergencyContact?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(Contacts.FirstOrDefault(contact => contact.Id == id)); public Task<EmergencyContact?> GetByLinkingCodeAsync(string linkingCode, CancellationToken cancellationToken) => Task.FromResult(Contacts.FirstOrDefault(contact => contact.LinkingCode == linkingCode)); public Task<int> CountActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Contacts.Count(contact => contact.UserId == userId && contact.IsActive)); public Task AddAsync(EmergencyContact contact, CancellationToken cancellationToken) { Contacts.Add(contact); return Task.CompletedTask; } public Task UpdateAsync(EmergencyContact contact, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryActivationCodeRepository : IDeviceActivationCodeRepository { public Task<IReadOnlyList<DeviceActivationCode>> GetActiveByUserIdAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DeviceActivationCode>>([]); public Task<DeviceActivationCode?> GetActiveCurrentByUserIdAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult<DeviceActivationCode?>(null); public Task<DeviceActivationCode?> GetByCodeAsync(string code, CancellationToken cancellationToken) => Task.FromResult<DeviceActivationCode?>(null); public Task AddAsync(DeviceActivationCode code, CancellationToken cancellationToken) => Task.CompletedTask; public Task UpdateAsync(DeviceActivationCode code, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryUserDeviceRepository : IUserDeviceRepository { public List<UserDevice> Devices { get; } = []; public Task<IReadOnlyList<UserDevice>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<UserDevice>>(Devices.Where(device => device.UserId == userId && device.IsActive).ToArray()); public Task<IReadOnlyList<UserDevice>> GetActiveByParentDeviceIdAsync(string parentDeviceId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<UserDevice>>(Devices.Where(device => device.ParentDeviceId == parentDeviceId && device.IsActive).ToArray()); public Task<UserDevice?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(Devices.FirstOrDefault(device => device.Id == id)); public Task<UserDevice?> GetByDeviceIdentifierHashAsync(string userId, string hash, DeviceType deviceType, CancellationToken cancellationToken) => Task.FromResult<UserDevice?>(null); public Task<int> CountActiveLinkedByUserIdAndTypeAsync(string userId, DeviceType deviceType, CancellationToken cancellationToken) => Task.FromResult(0); public Task<bool> HasActiveLinkedMobileAppAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Devices.Any(device => device.UserId == userId && device.DeviceType == DeviceType.MobileApp && device.IsActive && device.LinkStatus == DeviceLinkStatus.Linked)); public Task AddAsync(UserDevice device, CancellationToken cancellationToken) { Devices.Add(device); return Task.CompletedTask; } public Task UpdateAsync(UserDevice device, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryUserSubscriptionRepository : IUserSubscriptionRepository { public List<UserSubscription> Subscriptions { get; } = []; public Task<UserSubscription?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Subscriptions.FirstOrDefault(subscription => subscription.UserId == userId)); public Task<bool> HasActiveSubscriptionAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Subscriptions.Any(subscription => subscription.UserId == userId && subscription.Status == SubscriptionStatus.Active)); public Task AddAsync(UserSubscription subscription, CancellationToken cancellationToken) { Subscriptions.Add(subscription); return Task.CompletedTask; } public Task UpdateAsync(UserSubscription subscription, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryOnboardingConfirmationRepository : IOnboardingConfirmationRepository { public List<OnboardingConfirmation> Confirmations { get; } = []; public Task<OnboardingConfirmation?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Confirmations.FirstOrDefault(confirmation => confirmation.UserId == userId)); public Task AddAsync(OnboardingConfirmation confirmation, CancellationToken cancellationToken) { Confirmations.Add(confirmation); return Task.CompletedTask; } public Task UpdateAsync(OnboardingConfirmation confirmation, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryTripRepository : ITripRepository { public List<Trip> Trips { get; } = []; public Task<Trip?> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Trips.FirstOrDefault(trip => trip.UserId == userId && trip.Status == TripStatus.Active)); public Task<Trip?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(Trips.FirstOrDefault(trip => trip.Id == id)); public Task<IReadOnlyList<Trip>> ListByUserIdAsync(string userId, TripStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Trip>>(Trips.Where(trip => trip.UserId == userId && (!status.HasValue || trip.Status == status.Value)).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToArray()); public Task<long> CountByUserIdAsync(string userId, TripStatus? status, CancellationToken cancellationToken) => Task.FromResult((long)Trips.Count(trip => trip.UserId == userId && (!status.HasValue || trip.Status == status.Value))); public Task AddAsync(Trip trip, CancellationToken cancellationToken) { Trips.Add(trip); return Task.CompletedTask; } public Task UpdateAsync(Trip trip, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed record LoginEnvelope(bool Success, LoginResponse Data);
    private sealed record TripEnvelope(bool Success, StartTripResponse Data);
}
