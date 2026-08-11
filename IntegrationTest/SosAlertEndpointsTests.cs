using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MotoSOS.API.Modules.AlertDispatch.Application;
using MotoSOS.API.Modules.AlertDispatch.Domain;
using MotoSOS.API.Modules.Auth.Application;
using MotoSOS.API.Modules.Auth.Contracts;
using MotoSOS.API.Modules.Auth.Domain;
using MotoSOS.API.Modules.Devices.Application;
using MotoSOS.API.Modules.Devices.Domain;
using MotoSOS.API.Modules.EmergencyContacts.Application;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
using MotoSOS.API.Modules.Incidents.Application;
using MotoSOS.API.Modules.Incidents.Domain;
using MotoSOS.API.Modules.Notifications.Application;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.OfflineIngestion.Application;
using MotoSOS.API.Modules.OfflineIngestion.Domain;
using MotoSOS.API.Modules.Onboarding.Application;
using MotoSOS.API.Modules.Onboarding.Domain;
using MotoSOS.API.Modules.Plans.Application;
using MotoSOS.API.Modules.Plans.Domain;
using MotoSOS.API.Modules.Profiles.Application;
using MotoSOS.API.Modules.Profiles.Domain;
using MotoSOS.API.Modules.PushNotificationTokens.Application;
using MotoSOS.API.Modules.PushNotificationTokens.Domain;
using MotoSOS.API.Modules.SosAlerts.Contracts;
using MotoSOS.API.Modules.Trips.Application;
using MotoSOS.API.Modules.Trips.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;
using MotoSOS.API.Modules.Vehicles.Application;
using MotoSOS.API.Modules.Vehicles.Domain;

namespace IntegrationTest;

public sealed class SosAlertEndpointsTests
{
    private const string FcmToken = "internal-fcm-token-abcdefghijklmnopqrstuvwxyz";
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new(System.Text.Json.JsonSerializerDefaults.Web);

    [Fact]
    public async Task SosAlertRequiresAuthentication()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory(new Stores());
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/mobile/sos-alerts", Request("trip"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("Monitor")]
    [InlineData("Admin")]
    public async Task MonitorAndAdminCannotCreateSosAlert(string role)
    {
        var stores = new Stores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        await AuthenticateAsync(client, $"sos-{role}@example.com", role, stores);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/mobile/sos-alerts", Request("trip"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RiderCanCreateSosAlertWithSmsAttempt()
    {
        var stores = new Stores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        User user = await AuthenticateAsync(client, "sos-rider@example.com", "Rider", stores);
        Trip trip = SeedReady(stores, user.Id);

        SosEnvelope envelope = (await (await client.PostAsJsonAsync("/api/v1/mobile/sos-alerts", Request(trip.Id))).Content.ReadFromJsonAsync<SosEnvelope>())!;

        envelope.Success.Should().BeTrue();
        envelope.Data.Incident.TripId.Should().Be(trip.Id);
        envelope.Data.Incident.Status.Should().Be("Open");
        envelope.Data.AlertDispatch.IncidentId.Should().Be(envelope.Data.Incident.Id);
        envelope.Data.AlertDispatch.ContactsCount.Should().Be(1);
        envelope.Data.NotificationAttempts.Should().ContainSingle(attempt => attempt.Channel == "Sms" && attempt.Status == "Prepared" && attempt.Provider == "None");
        envelope.Data.Summary.SmsPrepared.Should().Be(1);
        envelope.Data.Summary.TotalPrepared.Should().Be(1);
        stores.Incidents.Items.Should().ContainSingle();
        stores.Alerts.Items.Should().ContainSingle();
        stores.Attempts.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task LinkedContactWithActiveFcmTokenCreatesPushAndPhoneKeepsSms()
    {
        var stores = new Stores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient rider = factory.CreateClient();
        User riderUser = await AuthenticateAsync(rider, "sos-push-rider@example.com", "Rider", stores);
        User monitor = await AuthenticateAsync(factory.CreateClient(), "sos-push-monitor@example.com", "Monitor", stores);
        Trip trip = SeedReady(stores, riderUser.Id);
        stores.Contacts.Items.Clear();
        stores.Contacts.Items.Add(new EmergencyContact { Id = "linked-contact", UserId = riderUser.Id, IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Linked, LinkedUserId = monitor.Id, FullName = "Linked Contact", PhoneNumber = "+52 555 555 5555", Email = monitor.Email, Priority = 1 });
        stores.Tokens.Items.Add(new PushNotificationToken { Id = "token", UserId = monitor.Id, Platform = PushTokenPlatform.Android, Channel = PushTokenChannel.Fcm, Status = PushNotificationTokenStatus.Active, TokenValue = FcmToken, TokenHash = "hash", TokenPreview = "intern****wxyz", LastSeenAtUtc = DateTimeOffset.UtcNow });

        string body = await (await rider.PostAsJsonAsync("/api/v1/mobile/sos-alerts", Request(trip.Id))).Content.ReadAsStringAsync();
        SosEnvelope envelope = System.Text.Json.JsonSerializer.Deserialize<SosEnvelope>(body, JsonOptions)!;

        envelope.Data.NotificationAttempts.Select(attempt => attempt.Channel).Should().Equal("Push", "Sms");
        envelope.Data.Summary.PushPrepared.Should().Be(1);
        envelope.Data.Summary.SmsPrepared.Should().Be(1);
        envelope.Data.Summary.TotalPrepared.Should().Be(2);
        body.Should().NotContain(FcmToken).And.NotContain("tokenValue").And.NotContain("tokenHash");
    }

    [Fact]
    public async Task SameRequestIsIdempotentAcrossIncidentDispatchAndAttempts()
    {
        var stores = new Stores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        User user = await AuthenticateAsync(client, "sos-idempotent@example.com", "Rider", stores);
        Trip trip = SeedReady(stores, user.Id);
        object request = Request(trip.Id, "11111111-1111-1111-1111-111111111111", "22222222-2222-2222-2222-222222222222");

        SosEnvelope first = (await (await client.PostAsJsonAsync("/api/v1/mobile/sos-alerts", request)).Content.ReadFromJsonAsync<SosEnvelope>())!;
        SosEnvelope second = (await (await client.PostAsJsonAsync("/api/v1/mobile/sos-alerts", request)).Content.ReadFromJsonAsync<SosEnvelope>())!;

        second.Data.Incident.Id.Should().Be(first.Data.Incident.Id);
        second.Data.AlertDispatch.Id.Should().Be(first.Data.AlertDispatch.Id);
        second.Data.NotificationAttempts.Select(attempt => attempt.Id).Should().Equal(first.Data.NotificationAttempts.Select(attempt => attempt.Id));
        stores.Incidents.Items.Should().ContainSingle();
        stores.Alerts.Items.Should().ContainSingle();
        stores.Attempts.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task ReadinessTripContactsAndValidationFailuresAreControlled()
    {
        var stores = new Stores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        User user = await AuthenticateAsync(client, "sos-failures@example.com", "Rider", stores);
        Trip trip = new() { Id = "trip-incomplete", UserId = user.Id, VehicleId = "vehicle", MobileDeviceId = "mobile", Status = TripStatus.Active, StartedAtUtc = DateTimeOffset.UtcNow, CreatedAtUtc = DateTimeOffset.UtcNow };
        stores.Trips.Items.Add(trip);

        HttpResponseMessage incomplete = await client.PostAsJsonAsync("/api/v1/mobile/sos-alerts", Request(trip.Id));
        string incompleteBody = await incomplete.Content.ReadAsStringAsync();
        SeedReady(stores, user.Id, tripId: "trip-ready");
        HttpResponseMessage missingTrip = await client.PostAsJsonAsync("/api/v1/mobile/sos-alerts", Request("missing-trip"));
        stores.Contacts.Items.Clear();
        HttpResponseMessage noContacts = await client.PostAsJsonAsync("/api/v1/mobile/sos-alerts", Request("trip-ready"));
        HttpResponseMessage invalidClientId = await client.PostAsJsonAsync("/api/v1/mobile/sos-alerts", Request("trip-ready", clientIncidentId: "incident-client-001"));

        incomplete.StatusCode.Should().Be(HttpStatusCode.BadRequest); incompleteBody.Should().Contain("onboarding_not_ready");
        missingTrip.StatusCode.Should().Be(HttpStatusCode.NotFound);
        noContacts.StatusCode.Should().Be(HttpStatusCode.BadRequest); (await noContacts.Content.ReadAsStringAsync()).Should().Contain("onboarding_not_ready");
        invalidClientId.StatusCode.Should().Be(HttpStatusCode.BadRequest); (await invalidClientId.Content.ReadAsStringAsync()).Should().Contain("validation_error");
    }

    private static object Request(string tripId, string? clientIncidentId = null, string? clientAlertRequestId = null) => new
    {
        tripId,
        clientIncidentId = clientIncidentId ?? Guid.NewGuid().ToString(),
        clientAlertRequestId = clientAlertRequestId ?? Guid.NewGuid().ToString(),
        incidentType = "CountdownTimeout",
        severity = "High",
        detectedAtUtc = DateTimeOffset.UtcNow,
        latitude = 19.4326,
        longitude = -99.1332,
        priority = "High",
        reason = "IncidentCreated",
        notes = "Caida detectada por sensores"
    };

    private static Trip SeedReady(Stores stores, string userId, string? tripId = null)
    {
        stores.Profiles.Items.Add(new DriverProfile { UserId = userId, CompletionStatus = ProfileCompletionStatus.Completed });
        stores.Vehicles.Items.Add(new DriverVehicle { Id = "vehicle", UserId = userId, IsActive = true, CompletionStatus = VehicleCompletionStatus.Completed });
        stores.Contacts.Items.Add(new EmergencyContact { Id = Guid.NewGuid().ToString(), UserId = userId, IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Invited, FullName = "Phone Contact", PhoneNumber = "+52 555 555 5555", Priority = 1 });
        stores.Devices.Items.Add(new UserDevice { Id = "mobile", UserId = userId, DeviceType = DeviceType.MobileApp, IsActive = true, LinkStatus = DeviceLinkStatus.Linked });
        stores.Subscriptions.Items.Add(new UserSubscription { UserId = userId, Status = SubscriptionStatus.Active, PlanTier = PlanTier.Basic });
        stores.Confirmations.Items.Add(new OnboardingConfirmation { UserId = userId, IsOperational = true });
        var trip = new Trip { Id = tripId ?? Guid.NewGuid().ToString(), UserId = userId, VehicleId = "vehicle", MobileDeviceId = "mobile", Status = TripStatus.Active, StartedAtUtc = DateTimeOffset.UtcNow, CreatedAtUtc = DateTimeOffset.UtcNow };
        stores.Trips.Items.Add(trip);
        return trip;
    }

    private static async Task<User> AuthenticateAsync(HttpClient client, string email, string role, Stores stores)
    {
        const string secret = "StrongPass1!";
        await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, secret, secret, "Moto User", "+52 555 555 5555", role == "Monitor" ? "Monitor" : "Rider", true));
        User user = stores.Users.Items.Single(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));
        if (role == "Admin") user.Role = UserRole.Admin;
        if (role == "Monitor") user.Role = UserRole.Monitor;
        LoginEnvelope login = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, secret))).Content.ReadFromJsonAsync<LoginEnvelope>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Data.AccessToken);
        return user;
    }

    private static WebApplicationFactory<Program> CreateFactory(Stores stores) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Issuer"] = "MotoSOS", ["Jwt:Audience"] = "MotoSOS.Clients", ["Jwt:Key"] = new string('N', 48), ["Jwt:AccessTokenMinutes"] = "15", ["Jwt:RefreshTokenDays"] = "7", ["Jwt:RefreshTokenRememberMeDays"] = "30", ["MongoDb:" + "Connection" + "String"] = string.Empty, ["MongoDb:DatabaseName"] = "MotoSOS_Test" }));
        builder.ConfigureTestServices(services => { services.AddSingleton<IUserRepository>(stores.Users); services.AddSingleton<IRefreshTokenRepository>(stores.RefreshTokens); services.AddSingleton<IDriverProfileRepository>(stores.Profiles); services.AddSingleton<IDriverVehicleRepository>(stores.Vehicles); services.AddSingleton<IEmergencyContactRepository>(stores.Contacts); services.AddSingleton<IDeviceActivationCodeRepository>(stores.Codes); services.AddSingleton<IUserDeviceRepository>(stores.Devices); services.AddSingleton<IUserSubscriptionRepository>(stores.Subscriptions); services.AddSingleton<IOnboardingConfirmationRepository>(stores.Confirmations); services.AddSingleton<ITripRepository>(stores.Trips); services.AddSingleton<IOfflineIngestionRepository>(stores.Offline); services.AddSingleton<IIncidentRepository>(stores.Incidents); services.AddSingleton<IAlertDispatchRepository>(stores.Alerts); services.AddSingleton<INotificationDeliveryAttemptRepository>(stores.Attempts); services.AddSingleton<IPushNotificationTokenRepository>(stores.Tokens); });
    });

    private sealed record LoginEnvelope(bool Success, LoginResponse Data);
    private sealed record SosEnvelope(bool Success, CreateSosAlertResponse Data);
    private sealed class Stores { public Users Users { get; } = new(); public RefreshTokens RefreshTokens { get; } = new(); public Profiles Profiles { get; } = new(); public Vehicles Vehicles { get; } = new(); public Contacts Contacts { get; } = new(); public Codes Codes { get; } = new(); public Devices Devices { get; } = new(); public Subscriptions Subscriptions { get; } = new(); public Confirmations Confirmations { get; } = new(); public Trips Trips { get; } = new(); public Offline Offline { get; } = new(); public Incidents Incidents { get; } = new(); public Alerts Alerts { get; } = new(); public Attempts Attempts { get; } = new(); public Tokens Tokens { get; } = new(); }
    private sealed class Users : IUserRepository { public List<User> Items { get; } = []; public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Id == id)); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase))); public Task AddAsync(User user, CancellationToken ct) { Items.Add(user); return Task.CompletedTask; } public Task UpdateAsync(User user, CancellationToken ct) => Task.CompletedTask; }
    private sealed class RefreshTokens : IRefreshTokenRepository { public List<RefreshToken> Items { get; } = []; public Task<RefreshToken?> GetByHashAsync(string h, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.TokenHash == h)); public Task AddAsync(RefreshToken t, CancellationToken ct) { Items.Add(t); return Task.CompletedTask; } public Task UpdateAsync(RefreshToken t, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Profiles : IDriverProfileRepository { public List<DriverProfile> Items { get; } = []; public Task<DriverProfile?> GetByUserIdAsync(string u, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(p => p.UserId == u)); public Task AddAsync(DriverProfile p, CancellationToken ct) { Items.Add(p); return Task.CompletedTask; } public Task UpdateAsync(DriverProfile p, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Vehicles : IDriverVehicleRepository { public List<DriverVehicle> Items { get; } = []; public Task<IReadOnlyList<DriverVehicle>> GetActiveByUserIdAsync(string u, CancellationToken ct) => Task.FromResult<IReadOnlyList<DriverVehicle>>(Items.Where(v => v.UserId == u && v.IsActive).ToArray()); public Task<DriverVehicle?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(v => v.Id == id)); public Task<int> CountActiveByUserIdAsync(string u, CancellationToken ct) => Task.FromResult(Items.Count(v => v.UserId == u && v.IsActive)); public Task AddAsync(DriverVehicle v, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(DriverVehicle v, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Contacts : IEmergencyContactRepository { public List<EmergencyContact> Items { get; } = []; public Task<IReadOnlyList<EmergencyContact>> GetActiveByUserIdAsync(string u, CancellationToken ct) => Task.FromResult<IReadOnlyList<EmergencyContact>>(Items.Where(c => c.UserId == u && c.IsActive).ToArray()); public Task<EmergencyContact?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(c => c.Id == id)); public Task<EmergencyContact?> GetByLinkingCodeAsync(string code, CancellationToken ct) => Task.FromResult<EmergencyContact?>(null); public Task<int> CountActiveByUserIdAsync(string u, CancellationToken ct) => Task.FromResult(Items.Count(c => c.UserId == u && c.IsActive)); public Task AddAsync(EmergencyContact c, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(EmergencyContact c, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Codes : IDeviceActivationCodeRepository { public Task<IReadOnlyList<DeviceActivationCode>> GetActiveByUserIdAsync(string u, DateTimeOffset n, CancellationToken ct) => Task.FromResult<IReadOnlyList<DeviceActivationCode>>([]); public Task<DeviceActivationCode?> GetActiveCurrentByUserIdAsync(string u, DateTimeOffset n, CancellationToken ct) => Task.FromResult<DeviceActivationCode?>(null); public Task<DeviceActivationCode?> GetByCodeAsync(string c, CancellationToken ct) => Task.FromResult<DeviceActivationCode?>(null); public Task AddAsync(DeviceActivationCode c, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(DeviceActivationCode c, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Devices : IUserDeviceRepository { public List<UserDevice> Items { get; } = []; public Task<IReadOnlyList<UserDevice>> GetActiveByUserIdAsync(string u, CancellationToken ct) => Task.FromResult<IReadOnlyList<UserDevice>>(Items.Where(d => d.UserId == u && d.IsActive).ToArray()); public Task<IReadOnlyList<UserDevice>> GetActiveByParentDeviceIdAsync(string p, CancellationToken ct) => Task.FromResult<IReadOnlyList<UserDevice>>([]); public Task<UserDevice?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(d => d.Id == id)); public Task<UserDevice?> GetByDeviceIdentifierHashAsync(string u, string h, DeviceType t, CancellationToken ct) => Task.FromResult<UserDevice?>(null); public Task<int> CountActiveLinkedByUserIdAndTypeAsync(string u, DeviceType t, CancellationToken ct) => Task.FromResult(0); public Task<bool> HasActiveLinkedMobileAppAsync(string u, CancellationToken ct) => Task.FromResult(Items.Any(d => d.UserId == u && d.DeviceType == DeviceType.MobileApp && d.IsActive && d.LinkStatus == DeviceLinkStatus.Linked)); public Task AddAsync(UserDevice d, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(UserDevice d, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Subscriptions : IUserSubscriptionRepository { public List<UserSubscription> Items { get; } = []; public Task<UserSubscription?> GetByUserIdAsync(string u, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(s => s.UserId == u)); public Task<bool> HasActiveSubscriptionAsync(string u, CancellationToken ct) => Task.FromResult(Items.Any(s => s.UserId == u && s.Status == SubscriptionStatus.Active)); public Task AddAsync(UserSubscription s, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(UserSubscription s, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Confirmations : IOnboardingConfirmationRepository { public List<OnboardingConfirmation> Items { get; } = []; public Task<OnboardingConfirmation?> GetByUserIdAsync(string u, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(c => c.UserId == u)); public Task AddAsync(OnboardingConfirmation c, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(OnboardingConfirmation c, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Trips : ITripRepository { public List<Trip> Items { get; } = []; public Task<Trip?> GetActiveByUserIdAsync(string u, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.UserId == u && t.Status == TripStatus.Active)); public Task<Trip?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.Id == id)); public Task<IReadOnlyList<Trip>> ListByUserIdAsync(string u, TripStatus? s, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<Trip>>(Items.Where(t => t.UserId == u && (!s.HasValue || t.Status == s.Value)).ToArray()); public Task<long> CountByUserIdAsync(string u, TripStatus? s, CancellationToken ct) => Task.FromResult((long)Items.Count(t => t.UserId == u && (!s.HasValue || t.Status == s.Value))); public Task AddAsync(Trip t, CancellationToken ct) { Items.Add(t); return Task.CompletedTask; } public Task UpdateAsync(Trip t, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Offline : IOfflineIngestionRepository { public Task<OfflineIngestionRecord?> GetByIdempotencyKeyAsync(string k, CancellationToken ct) => Task.FromResult<OfflineIngestionRecord?>(null); public Task<(OfflineIngestionRecord Record, bool IsDuplicate)> AddOrGetDuplicateAsync(OfflineIngestionRecord r, CancellationToken ct) => Task.FromResult((r, false)); }
    private sealed class Incidents : IIncidentRepository { public List<Incident> Items { get; } = []; public Task<Incident?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id)); public Task<Incident?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.IdempotencyKey == key)); public Task<(Incident Incident, bool IsDuplicate)> AddOrGetDuplicateAsync(Incident incident, CancellationToken ct) { Incident? existing = Items.FirstOrDefault(i => i.IdempotencyKey == incident.IdempotencyKey); if (existing is not null) return Task.FromResult((existing, true)); Items.Add(incident); return Task.FromResult((incident, false)); } public Task<IReadOnlyList<Incident>> ListByUserIdAsync(string u, IncidentStatus? s, string? t, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<Incident>>(Items.Where(i => i.UserId == u).ToArray()); public Task<long> CountByUserIdAsync(string u, IncidentStatus? s, string? t, CancellationToken ct) => Task.FromResult((long)Items.Count(i => i.UserId == u)); public Task UpdateAsync(Incident i, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Alerts : IAlertDispatchRepository { public List<AlertDispatchRequest> Items { get; } = []; public Task<AlertDispatchRequest?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.Id == id)); public Task<AlertDispatchRequest?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.IdempotencyKey == key)); public Task<(AlertDispatchRequest AlertDispatch, bool IsDuplicate)> AddOrGetDuplicateAsync(AlertDispatchRequest alert, CancellationToken ct) { AlertDispatchRequest? existing = Items.FirstOrDefault(a => a.IdempotencyKey == alert.IdempotencyKey); if (existing is not null) return Task.FromResult((existing, true)); Items.Add(alert); return Task.FromResult((alert, false)); } public Task<IReadOnlyList<AlertDispatchRequest>> ListByUserIdAsync(string u, AlertDispatchStatus? s, string? i, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertDispatchRequest>>(Items.Where(a => a.UserId == u).ToArray()); public Task<long> CountByUserIdAsync(string u, AlertDispatchStatus? s, string? i, CancellationToken ct) => Task.FromResult((long)Items.Count(a => a.UserId == u)); public Task UpdateAsync(AlertDispatchRequest alert, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Attempts : INotificationDeliveryAttemptRepository { public List<NotificationDeliveryAttempt> Items { get; } = []; public Task<NotificationDeliveryAttempt?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.Id == id)); public Task<NotificationDeliveryAttempt?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.IdempotencyKey == key)); public Task<(NotificationDeliveryAttempt Attempt, bool IsDuplicate)> AddOrGetDuplicateAsync(NotificationDeliveryAttempt attempt, CancellationToken ct) { NotificationDeliveryAttempt? existing = Items.FirstOrDefault(a => a.IdempotencyKey == attempt.IdempotencyKey); if (existing is not null) return Task.FromResult((existing, true)); Items.Add(attempt); return Task.FromResult((attempt, false)); } public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByUserIdAsync(string u, string? a, string? i, NotificationDeliveryStatus? s, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>(Items.Where(x => x.UserId == u).ToArray()); public Task<long> CountByUserIdAsync(string u, string? a, string? i, NotificationDeliveryStatus? s, CancellationToken ct) => Task.FromResult((long)Items.Count(x => x.UserId == u)); public Task UpdateAsync(NotificationDeliveryAttempt attempt, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Tokens : IPushNotificationTokenRepository { public List<PushNotificationToken> Items { get; } = []; public Task<PushNotificationToken?> GetLatestActiveFcmByUserIdAsync(string userId, CancellationToken ct) => Task.FromResult(Items.Where(t => t.UserId == userId && t.Status == PushNotificationTokenStatus.Active && t.Channel == PushTokenChannel.Fcm && t.Platform is PushTokenPlatform.Android or PushTokenPlatform.Web).OrderByDescending(t => t.LastSeenAtUtc).FirstOrDefault()); public Task<PushNotificationToken?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.Id == id)); public Task<PushNotificationToken?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.IdempotencyKey == key)); public Task<(PushNotificationToken Token, bool IsDuplicate)> AddOrGetDuplicateAsync(PushNotificationToken token, CancellationToken ct) { Items.Add(token); return Task.FromResult((token, false)); } public Task UpdateAsync(PushNotificationToken token, CancellationToken ct) => Task.CompletedTask; public Task<long> RevokeActiveTokensForScopeAsync(string userId, PushTokenPlatform platform, PushTokenChannel channel, string? deviceId, DateTimeOffset now, CancellationToken ct) => Task.FromResult(0L); public Task<IReadOnlyList<PushNotificationToken>> ListByUserIdAsync(string userId, PushNotificationTokenQuery query, CancellationToken ct) => Task.FromResult<IReadOnlyList<PushNotificationToken>>(Items.Where(t => t.UserId == userId).ToArray()); public Task<long> CountByUserIdAsync(string userId, PushNotificationTokenQuery query, CancellationToken ct) => Task.FromResult((long)Items.Count(t => t.UserId == userId)); public Task<PushNotificationTokenStatusSummary> GetStatusByUserIdAsync(string userId, CancellationToken ct) => Task.FromResult(new PushNotificationTokenStatusSummary(0, 0, false, false, false, false, null)); public Task<IReadOnlyList<PushNotificationToken>> ListAsync(PushNotificationTokenQuery query, CancellationToken ct) => Task.FromResult<IReadOnlyList<PushNotificationToken>>(Items); public Task<long> CountAsync(PushNotificationTokenQuery query, CancellationToken ct) => Task.FromResult((long)Items.Count); }
}
