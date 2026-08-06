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
using MotoSOS.API.Modules.Notifications.Contracts;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.OfflineIngestion.Application;
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

public sealed class NotificationSecurityTests
{
    [Fact]
    public async Task NotificationResponsesDoNotExposeSensitiveOrProviderDataAndBlockCrossUserAccess()
    {
        var stores = new Stores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient owner = factory.CreateClient(); HttpClient attacker = factory.CreateClient(); User ownerUser = await AuthenticateAsync(owner, "notif-owner-sec@example.com", "Rider", stores); User attackerUser = await AuthenticateAsync(attacker, "notif-attacker-sec@example.com", "Rider", stores); AlertDispatchRequest ownerAlert = SeedReadyAlert(stores, ownerUser.Id); SeedReadyAlert(stores, attackerUser.Id); stores.Devices.Items.First(d => d.UserId == ownerUser.Id).DeviceIdentifierHash = "hashed-device-id";
        string body = await (await owner.PostAsJsonAsync("/api/v1/notifications/delivery-attempts/prepare", new PrepareNotificationAttemptsRequest(ownerAlert.Id, "prepared"))).Content.ReadAsStringAsync();
        string attemptId = stores.Attempts.Items.Single().Id;

        body.Should().Contain("attempts");
        body.Should().NotContain("passwordHash");
        body.Should().NotContain("refreshToken");
        body.Should().NotContain("accessToken");
        body.Should().NotContain("deviceIdentifier");
        body.Should().NotContain("DeviceIdentifierHash");
        body.Should().NotContain("hashed-device-id");
        body.Should().NotContain("GooglePlay");
        body.Should().NotContain("Stripe");
        body.Should().NotContain("Payment");
        body.Should().NotContain("Twilio");
        body.Should().NotContain("SendGrid");
        body.Should().NotContain("FCM");
        (await attacker.PostAsJsonAsync("/api/v1/notifications/delivery-attempts/prepare", new PrepareNotificationAttemptsRequest(ownerAlert.Id, null))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await attacker.GetAsync($"/api/v1/notifications/delivery-attempts/{attemptId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await attacker.PostAsJsonAsync($"/api/v1/notifications/delivery-attempts/{attemptId}/cancel", new CancelNotificationAttemptRequest(null, null))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("Monitor")]
    [InlineData("Admin")]
    public async Task MonitorAndAdminCannotUseNotificationsApi(string role)
    {
        var stores = new Stores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient client = factory.CreateClient(); await AuthenticateAsync(client, $"notif-{role}@example.com", role, stores);
        (await client.GetAsync("/api/v1/notifications/delivery-attempts")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static AlertDispatchRequest SeedReadyAlert(Stores s, string userId) { s.Profiles.Items.Add(new DriverProfile { UserId = userId, CompletionStatus = ProfileCompletionStatus.Completed }); s.Vehicles.Items.Add(new DriverVehicle { UserId = userId, IsActive = true, CompletionStatus = VehicleCompletionStatus.Completed }); s.Contacts.Items.Add(new EmergencyContact { UserId = userId, IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Invited }); s.Devices.Items.Add(new UserDevice { UserId = userId, DeviceType = DeviceType.MobileApp, IsActive = true, LinkStatus = DeviceLinkStatus.Linked }); s.Subscriptions.Items.Add(new UserSubscription { UserId = userId, Status = SubscriptionStatus.Active, PlanTier = PlanTier.Basic }); s.Confirmations.Items.Add(new OnboardingConfirmation { UserId = userId, IsOperational = true }); AlertDispatchRequest alert = new() { UserId = userId, IncidentId = "incident", TripId = "trip", VehicleId = "vehicle", MobileDeviceId = "mobile", ClientAlertRequestId = Guid.NewGuid().ToString(), IdempotencyKey = Guid.NewGuid().ToString(), Priority = AlertDispatchPriority.High, Reason = AlertDispatchReason.IncidentCreated, Status = AlertDispatchStatus.PendingDispatch, RequestedAtUtc = DateTimeOffset.UtcNow, CreatedAtUtc = DateTimeOffset.UtcNow, ContactsSnapshot = [new AlertContactSnapshot { EmergencyContactId = Guid.NewGuid().ToString(), PhoneNumber = "555", InvitationStatus = EmergencyContactInvitationStatus.Invited }] }; s.Alerts.Items.Add(alert); return alert; }
    private static async Task<User> AuthenticateAsync(HttpClient client, string email, string role, Stores stores) { RegisterRequest register = new(email, "StrongPass1!", "StrongPass1!", "Moto Rider", "+52 555 555 5555", "Rider", true); await client.PostAsJsonAsync("/api/v1/auth/register", register); User user = stores.Users.Items.Single(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)); if (role == "Admin") user.Role = UserRole.Admin; if (role == "Monitor") user.Role = UserRole.Monitor; LoginEnvelope login = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, register.Password))).Content.ReadFromJsonAsync<LoginEnvelope>())!; client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Data.AccessToken); return user; }
    private static WebApplicationFactory<Program> CreateFactory(Stores stores) => new WebApplicationFactory<Program>().WithWebHostBuilder(b => { b.UseEnvironment("Testing"); b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Issuer"] = "MotoSOS", ["Jwt:Audience"] = "MotoSOS.Clients", ["Jwt:Key"] = new string('M', 48), ["Jwt:AccessTokenMinutes"] = "15", ["Jwt:RefreshTokenDays"] = "7", ["Jwt:RefreshTokenRememberMeDays"] = "30", ["MongoDb:ConnectionString"] = string.Empty, ["MongoDb:DatabaseName"] = "MotoSOS_Test" })); b.ConfigureTestServices(s => { s.AddSingleton<IUserRepository>(stores.Users); s.AddSingleton<IRefreshTokenRepository>(stores.RefreshTokens); s.AddSingleton<IDriverProfileRepository>(stores.Profiles); s.AddSingleton<IDriverVehicleRepository>(stores.Vehicles); s.AddSingleton<IEmergencyContactRepository>(stores.Contacts); s.AddSingleton<IDeviceActivationCodeRepository>(stores.Codes); s.AddSingleton<IUserDeviceRepository>(stores.Devices); s.AddSingleton<IUserSubscriptionRepository>(stores.Subscriptions); s.AddSingleton<IOnboardingConfirmationRepository>(stores.Confirmations); s.AddSingleton<ITripRepository>(stores.Trips); s.AddSingleton<IOfflineIngestionRepository>(stores.Offline); s.AddSingleton<IIncidentRepository>(stores.Incidents); s.AddSingleton<IAlertDispatchRepository>(stores.Alerts); s.AddSingleton<INotificationDeliveryAttemptRepository>(stores.Attempts); }); });
    private sealed class Stores { public Users Users { get; } = new(); public RefreshTokens RefreshTokens { get; } = new(); public Profiles Profiles { get; } = new(); public Vehicles Vehicles { get; } = new(); public Contacts Contacts { get; } = new(); public Codes Codes { get; } = new(); public Devices Devices { get; } = new(); public Subscriptions Subscriptions { get; } = new(); public Confirmations Confirmations { get; } = new(); public Trips Trips { get; } = new(); public Offline Offline { get; } = new(); public Incidents Incidents { get; } = new(); public Alerts Alerts { get; } = new(); public Attempts Attempts { get; } = new(); }
    private sealed record LoginEnvelope(bool Success, LoginResponse Data);
    private sealed class Users : IUserRepository { public List<User> Items { get; } = []; public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Id == id)); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase))); public Task AddAsync(User user, CancellationToken ct) { Items.Add(user); return Task.CompletedTask; } public Task UpdateAsync(User user, CancellationToken ct) => Task.CompletedTask; }
    private sealed class RefreshTokens : IRefreshTokenRepository { public List<RefreshToken> Tokens { get; } = []; public Task<RefreshToken?> GetByHashAsync(string h, CancellationToken ct) => Task.FromResult(Tokens.FirstOrDefault(t => t.TokenHash == h)); public Task AddAsync(RefreshToken t, CancellationToken ct) { Tokens.Add(t); return Task.CompletedTask; } public Task UpdateAsync(RefreshToken t, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Profiles : IDriverProfileRepository { public List<DriverProfile> Items { get; } = []; public Task<DriverProfile?> GetByUserIdAsync(string u, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(p => p.UserId == u)); public Task AddAsync(DriverProfile p, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(DriverProfile p, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Vehicles : IDriverVehicleRepository { public List<DriverVehicle> Items { get; } = []; public Task<IReadOnlyList<DriverVehicle>> GetActiveByUserIdAsync(string u, CancellationToken ct) => Task.FromResult<IReadOnlyList<DriverVehicle>>(Items.Where(v => v.UserId == u && v.IsActive).ToArray()); public Task<DriverVehicle?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(v => v.Id == id)); public Task<int> CountActiveByUserIdAsync(string u, CancellationToken ct) => Task.FromResult(Items.Count(v => v.UserId == u && v.IsActive)); public Task AddAsync(DriverVehicle v, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(DriverVehicle v, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Contacts : IEmergencyContactRepository { public List<EmergencyContact> Items { get; } = []; public Task<IReadOnlyList<EmergencyContact>> GetActiveByUserIdAsync(string u, CancellationToken ct) => Task.FromResult<IReadOnlyList<EmergencyContact>>(Items.Where(c => c.UserId == u && c.IsActive).ToArray()); public Task<EmergencyContact?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult<EmergencyContact?>(null); public Task<EmergencyContact?> GetByLinkingCodeAsync(string code, CancellationToken ct) => Task.FromResult<EmergencyContact?>(null); public Task<int> CountActiveByUserIdAsync(string u, CancellationToken ct) => Task.FromResult(0); public Task AddAsync(EmergencyContact c, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(EmergencyContact c, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Codes : IDeviceActivationCodeRepository { public Task<IReadOnlyList<DeviceActivationCode>> GetActiveByUserIdAsync(string u, DateTimeOffset n, CancellationToken ct) => Task.FromResult<IReadOnlyList<DeviceActivationCode>>([]); public Task<DeviceActivationCode?> GetActiveCurrentByUserIdAsync(string u, DateTimeOffset n, CancellationToken ct) => Task.FromResult<DeviceActivationCode?>(null); public Task<DeviceActivationCode?> GetByCodeAsync(string c, CancellationToken ct) => Task.FromResult<DeviceActivationCode?>(null); public Task AddAsync(DeviceActivationCode c, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(DeviceActivationCode c, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Devices : IUserDeviceRepository { public List<UserDevice> Items { get; } = []; public Task<IReadOnlyList<UserDevice>> GetActiveByUserIdAsync(string u, CancellationToken ct) => Task.FromResult<IReadOnlyList<UserDevice>>(Items.Where(d => d.UserId == u && d.IsActive).ToArray()); public Task<IReadOnlyList<UserDevice>> GetActiveByParentDeviceIdAsync(string p, CancellationToken ct) => Task.FromResult<IReadOnlyList<UserDevice>>([]); public Task<UserDevice?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(d => d.Id == id)); public Task<UserDevice?> GetByDeviceIdentifierHashAsync(string u, string h, DeviceType t, CancellationToken ct) => Task.FromResult<UserDevice?>(null); public Task<int> CountActiveLinkedByUserIdAndTypeAsync(string u, DeviceType t, CancellationToken ct) => Task.FromResult(0); public Task<bool> HasActiveLinkedMobileAppAsync(string u, CancellationToken ct) => Task.FromResult(Items.Any(d => d.UserId == u && d.DeviceType == DeviceType.MobileApp && d.IsActive && d.LinkStatus == DeviceLinkStatus.Linked)); public Task AddAsync(UserDevice d, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(UserDevice d, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Subscriptions : IUserSubscriptionRepository { public List<UserSubscription> Items { get; } = []; public Task<UserSubscription?> GetByUserIdAsync(string u, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(s => s.UserId == u)); public Task<bool> HasActiveSubscriptionAsync(string u, CancellationToken ct) => Task.FromResult(Items.Any(s => s.UserId == u && s.Status == SubscriptionStatus.Active)); public Task AddAsync(UserSubscription s, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(UserSubscription s, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Confirmations : IOnboardingConfirmationRepository { public List<OnboardingConfirmation> Items { get; } = []; public Task<OnboardingConfirmation?> GetByUserIdAsync(string u, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(c => c.UserId == u)); public Task AddAsync(OnboardingConfirmation c, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(OnboardingConfirmation c, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Trips : ITripRepository { public Task<Trip?> GetActiveByUserIdAsync(string u, CancellationToken ct) => Task.FromResult<Trip?>(null); public Task<Trip?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult<Trip?>(null); public Task<IReadOnlyList<Trip>> ListByUserIdAsync(string u, TripStatus? s, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<Trip>>([]); public Task<long> CountByUserIdAsync(string u, TripStatus? s, CancellationToken ct) => Task.FromResult(0L); public Task AddAsync(Trip t, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(Trip t, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Offline : IOfflineIngestionRepository { public Task<OfflineIngestionRecord?> GetByIdempotencyKeyAsync(string k, CancellationToken ct) => Task.FromResult<OfflineIngestionRecord?>(null); public Task<(OfflineIngestionRecord Record, bool IsDuplicate)> AddOrGetDuplicateAsync(OfflineIngestionRecord r, CancellationToken ct) => Task.FromResult((r, false)); }
    private sealed class Incidents : IIncidentRepository { public Task<Incident?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult<Incident?>(null); public Task<Incident?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult<Incident?>(null); public Task<(Incident Incident, bool IsDuplicate)> AddOrGetDuplicateAsync(Incident incident, CancellationToken ct) => Task.FromResult((incident, false)); public Task<IReadOnlyList<Incident>> ListByUserIdAsync(string u, IncidentStatus? s, string? t, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<Incident>>([]); public Task<long> CountByUserIdAsync(string u, IncidentStatus? s, string? t, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(Incident i, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Alerts : IAlertDispatchRepository { public List<AlertDispatchRequest> Items { get; } = []; public Task<AlertDispatchRequest?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.Id == id)); public Task<AlertDispatchRequest?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.IdempotencyKey == key)); public Task<(AlertDispatchRequest AlertDispatch, bool IsDuplicate)> AddOrGetDuplicateAsync(AlertDispatchRequest alert, CancellationToken ct) => Task.FromResult((alert, false)); public Task<IReadOnlyList<AlertDispatchRequest>> ListByUserIdAsync(string u, AlertDispatchStatus? s, string? i, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertDispatchRequest>>([]); public Task<long> CountByUserIdAsync(string u, AlertDispatchStatus? s, string? i, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(AlertDispatchRequest alert, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Attempts : INotificationDeliveryAttemptRepository { public List<NotificationDeliveryAttempt> Items { get; } = []; public Task<NotificationDeliveryAttempt?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.Id == id)); public Task<NotificationDeliveryAttempt?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.IdempotencyKey == key)); public Task<(NotificationDeliveryAttempt Attempt, bool IsDuplicate)> AddOrGetDuplicateAsync(NotificationDeliveryAttempt attempt, CancellationToken ct) { Items.Add(attempt); return Task.FromResult((attempt, false)); } public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByUserIdAsync(string u, string? a, string? i, NotificationDeliveryStatus? s, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>(Items.Where(x => x.UserId == u).ToArray()); public Task<long> CountByUserIdAsync(string u, string? a, string? i, NotificationDeliveryStatus? s, CancellationToken ct) => Task.FromResult((long)Items.Count(x => x.UserId == u)); public Task UpdateAsync(NotificationDeliveryAttempt attempt, CancellationToken ct) => Task.CompletedTask; }
}
