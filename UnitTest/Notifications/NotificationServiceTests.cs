using FluentAssertions;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AlertDispatch.Application;
using MotoSOS.API.Modules.AlertDispatch.Domain;
using MotoSOS.API.Modules.EmergencyContacts.Application;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
using MotoSOS.API.Modules.NotificationPreferences.Application;
using MotoSOS.API.Modules.NotificationPreferences.Domain;
using MotoSOS.API.Modules.Notifications.Application;
using MotoSOS.API.Modules.Notifications.Contracts;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.Onboarding.Application;
using MotoSOS.API.Modules.Onboarding.Contracts;
using MotoSOS.API.Modules.PushNotificationTokens.Application;
using MotoSOS.API.Modules.PushNotificationTokens.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace UnitTest.Notifications;

public sealed class NotificationServiceTests
{
    [Fact]
    public async Task RiderCanPrepareAttemptsFromOwnPendingAlertDispatchUsingSnapshotChannels()
    {
        User user = User(UserRole.Rider); AlertDispatchRequest alert = Alert(user.Id, AlertDispatchStatus.PendingDispatch, [Contact("c1", phone: "555", email: "a@example.com"), Contact("c2", email: "b@example.com")]); var attempts = new Attempts();
        PrepareNotificationAttemptsResponse response = await Service(user, alerts: new Alerts(alert), attempts: attempts).PrepareAsync(user.Id, new PrepareNotificationAttemptsRequest(alert.Id, "notes"), CancellationToken.None);
        response.Attempts.Should().HaveCount(2);
        response.Attempts.Select(a => a.Channel).Should().Equal("Sms", "Email");
        attempts.Items.Should().OnlyContain(a => a.Status == NotificationDeliveryStatus.Prepared && a.Provider == NotificationProvider.None);
    }

    [Fact]
    public async Task PrepareRejectsIncompleteOnboardingForeignNotReadyCompletedAndNoChannels()
    {
        User user = User(UserRole.Rider); User other = User(UserRole.Rider);
        AlertDispatchRequest own = Alert(user.Id, AlertDispatchStatus.PendingDispatch, [Contact("c")]);
        await Assert.ThrowsAsync<OnboardingNotReadyAppException>(() => Service(user, ready: false, alerts: new Alerts(own)).PrepareAsync(user.Id, new PrepareNotificationAttemptsRequest(own.Id, null), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundAppException>(() => Service(user, alerts: new Alerts(Alert(other.Id, AlertDispatchStatus.PendingDispatch, [Contact("c", phone: "1")]))).PrepareAsync(user.Id, new PrepareNotificationAttemptsRequest("missing", null), CancellationToken.None));
        AlertDispatchRequest cancelled = Alert(user.Id, AlertDispatchStatus.Cancelled, [Contact("c", phone: "1")]); await Assert.ThrowsAsync<AlertDispatchNotReadyAppException>(() => Service(user, alerts: new Alerts(cancelled)).PrepareAsync(user.Id, new PrepareNotificationAttemptsRequest(cancelled.Id, null), CancellationToken.None));
        AlertDispatchRequest completed = Alert(user.Id, AlertDispatchStatus.Completed, [Contact("c", phone: "1")]); await Assert.ThrowsAsync<AlertDispatchAlreadyCompletedAppException>(() => Service(user, alerts: new Alerts(completed)).PrepareAsync(user.Id, new PrepareNotificationAttemptsRequest(completed.Id, null), CancellationToken.None));
        await Assert.ThrowsAsync<NotificationNotAllowedAppException>(() => Service(user, alerts: new Alerts(own)).PrepareAsync(user.Id, new PrepareNotificationAttemptsRequest(own.Id, null), CancellationToken.None));
        AlertDispatchRequest empty = Alert(user.Id, AlertDispatchStatus.PendingDispatch, []); await Assert.ThrowsAsync<NotificationNotAllowedAppException>(() => Service(user, alerts: new Alerts(empty)).PrepareAsync(user.Id, new PrepareNotificationAttemptsRequest(empty.Id, null), CancellationToken.None));
    }

    [Fact]
    public async Task PrepareIsIdempotentAndDoesNotDuplicate()
    {
        User user = User(UserRole.Rider); AlertDispatchRequest alert = Alert(user.Id, AlertDispatchStatus.PendingDispatch, [Contact("c1", phone: "555")]); var attempts = new Attempts(); NotificationService service = Service(user, alerts: new Alerts(alert), attempts: attempts);
        PrepareNotificationAttemptsResponse first = await service.PrepareAsync(user.Id, new PrepareNotificationAttemptsRequest(alert.Id, null), CancellationToken.None);
        PrepareNotificationAttemptsResponse duplicate = await service.PrepareAsync(user.Id, new PrepareNotificationAttemptsRequest(alert.Id, null), CancellationToken.None);
        duplicate.Attempts.Single().Id.Should().Be(first.Attempts.Single().Id);
        attempts.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task PrepareCreatesPushAttemptForLinkedContactWithActiveFcmToken()
    {
        User user = User(UserRole.Rider);
        AlertDispatchRequest alert = Alert(user.Id, AlertDispatchStatus.PendingDispatch, [Contact("contact", phone: "555", email: "monitor@example.com", EmergencyContactInvitationStatus.Linked)]);
        var contact = new EmergencyContact { Id = "contact", UserId = user.Id, IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Linked, LinkedUserId = "monitor", PhoneNumber = "555", Email = "monitor@example.com" };
        var token = new PushNotificationToken { UserId = "monitor", Status = PushNotificationTokenStatus.Active, Channel = PushTokenChannel.Fcm, Platform = PushTokenPlatform.Android, TokenValue = "fcm-token", LastSeenAtUtc = Now };
        var attempts = new Attempts();

        PrepareNotificationAttemptsResponse response = await Service(user, alerts: new Alerts(alert), attempts: attempts, contacts: new Contacts(contact), pushTokens: new PushTokens(token)).PrepareAsync(user.Id, new PrepareNotificationAttemptsRequest(alert.Id, null), CancellationToken.None);

        response.Attempts.Select(a => a.Channel).Should().Equal("Push");
        attempts.Items.Should().Contain(a => a.Channel == NotificationChannel.Push && a.EmergencyContactId == "contact");
    }

    [Fact]
    public async Task PrepareRespectsLinkedMonitorChannelPreferences()
    {
        User user = User(UserRole.Rider);
        AlertDispatchRequest alert = Alert(user.Id, AlertDispatchStatus.PendingDispatch, [Contact("contact", phone: "555", email: "monitor@example.com", EmergencyContactInvitationStatus.Linked)]);
        var contact = new EmergencyContact { Id = "contact", UserId = user.Id, IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Linked, LinkedUserId = "monitor", PhoneNumber = "555", Email = "monitor@example.com" };
        var token = new PushNotificationToken { UserId = "monitor", Status = PushNotificationTokenStatus.Active, Channel = PushTokenChannel.Fcm, Platform = PushTokenPlatform.Android, TokenValue = "fcm-token", LastSeenAtUtc = Now };
        NotificationPreference preference = NotificationPreferenceService.CreateDefault("monitor", Now);
        preference.PushEnabled = false;
        preference.SmsEnabled = false;
        var attempts = new Attempts();

        await Assert.ThrowsAsync<NotificationNotAllowedAppException>(() => Service(user, alerts: new Alerts(alert), attempts: attempts, contacts: new Contacts(contact), pushTokens: new PushTokens(token), preferences: new Preferences(preference)).PrepareAsync(user.Id, new PrepareNotificationAttemptsRequest(alert.Id, null), CancellationToken.None));

        attempts.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task PrepareDoesNotApplyPreferencesToUnlinkedSnapshotContacts()
    {
        User user = User(UserRole.Rider);
        AlertDispatchRequest alert = Alert(user.Id, AlertDispatchStatus.PendingDispatch, [Contact("contact", phone: "555")]);
        var attempts = new Attempts();

        PrepareNotificationAttemptsResponse response = await Service(user, alerts: new Alerts(alert), attempts: attempts, preferences: new Preferences()).PrepareAsync(user.Id, new PrepareNotificationAttemptsRequest(alert.Id, null), CancellationToken.None);

        response.Attempts.Should().ContainSingle(a => a.Channel == "Sms");
        attempts.Items.Should().ContainSingle(a => a.Channel == NotificationChannel.Sms);
    }

    [Fact]
    public async Task PrepareCreatesEmailAttemptForLinkedMonitorWhenEmailPreferenceEnabled()
    {
        User user = User(UserRole.Rider);
        AlertDispatchRequest alert = Alert(user.Id, AlertDispatchStatus.PendingDispatch, [Contact("contact", email: "monitor@example.com", status: EmergencyContactInvitationStatus.Linked)]);
        var contact = new EmergencyContact { Id = "contact", UserId = user.Id, IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Linked, LinkedUserId = "monitor", Email = "monitor@example.com" };
        NotificationPreference preference = NotificationPreferenceService.CreateDefault("monitor", Now);
        preference.PushEnabled = false;
        preference.EmailEnabled = true;
        var attempts = new Attempts();

        PrepareNotificationAttemptsResponse response = await Service(user, alerts: new Alerts(alert), attempts: attempts, contacts: new Contacts(contact), preferences: new Preferences(preference)).PrepareAsync(user.Id, new PrepareNotificationAttemptsRequest(alert.Id, null), CancellationToken.None);

        response.Attempts.Should().ContainSingle(a => a.Channel == "Email");
        attempts.Items.Should().ContainSingle(a => a.Channel == NotificationChannel.Email);
    }

    [Fact]
    public async Task PrepareSuppressesEmailAttemptForLinkedMonitorWhenEmailPreferenceDisabled()
    {
        User user = User(UserRole.Rider);
        AlertDispatchRequest alert = Alert(user.Id, AlertDispatchStatus.PendingDispatch, [Contact("contact", email: "monitor@example.com", status: EmergencyContactInvitationStatus.Linked)]);
        var contact = new EmergencyContact { Id = "contact", UserId = user.Id, IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Linked, LinkedUserId = "monitor", Email = "monitor@example.com" };
        NotificationPreference preference = NotificationPreferenceService.CreateDefault("monitor", Now);
        preference.PushEnabled = false;
        preference.EmailEnabled = false;
        var attempts = new Attempts();

        await Assert.ThrowsAsync<NotificationNotAllowedAppException>(() => Service(user, alerts: new Alerts(alert), attempts: attempts, contacts: new Contacts(contact), preferences: new Preferences(preference)).PrepareAsync(user.Id, new PrepareNotificationAttemptsRequest(alert.Id, null), CancellationToken.None));

        attempts.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task PrepareCreatesSmsAttemptForLinkedMonitorWhenSmsPreferenceEnabled()
    {
        User user = User(UserRole.Rider);
        AlertDispatchRequest alert = Alert(user.Id, AlertDispatchStatus.PendingDispatch, [Contact("contact", phone: "555", status: EmergencyContactInvitationStatus.Linked)]);
        var contact = new EmergencyContact { Id = "contact", UserId = user.Id, IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Linked, LinkedUserId = "monitor", PhoneNumber = "555" };
        NotificationPreference preference = NotificationPreferenceService.CreateDefault("monitor", Now);
        preference.PushEnabled = false;
        preference.SmsEnabled = true;
        var attempts = new Attempts();

        PrepareNotificationAttemptsResponse response = await Service(user, alerts: new Alerts(alert), attempts: attempts, contacts: new Contacts(contact), preferences: new Preferences(preference)).PrepareAsync(user.Id, new PrepareNotificationAttemptsRequest(alert.Id, null), CancellationToken.None);

        response.Attempts.Should().ContainSingle(a => a.Channel == "Sms");
        attempts.Items.Should().ContainSingle(a => a.Channel == NotificationChannel.Sms);
    }

    [Fact]
    public async Task PrepareSuppressesSmsAttemptForLinkedMonitorWhenSmsPreferenceDisabled()
    {
        User user = User(UserRole.Rider);
        AlertDispatchRequest alert = Alert(user.Id, AlertDispatchStatus.PendingDispatch, [Contact("contact", phone: "555", status: EmergencyContactInvitationStatus.Linked)]);
        var contact = new EmergencyContact { Id = "contact", UserId = user.Id, IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Linked, LinkedUserId = "monitor", PhoneNumber = "555" };
        NotificationPreference preference = NotificationPreferenceService.CreateDefault("monitor", Now);
        preference.PushEnabled = false;
        preference.SmsEnabled = false;
        var attempts = new Attempts();

        await Assert.ThrowsAsync<NotificationNotAllowedAppException>(() => Service(user, alerts: new Alerts(alert), attempts: attempts, contacts: new Contacts(contact), preferences: new Preferences(preference)).PrepareAsync(user.Id, new PrepareNotificationAttemptsRequest(alert.Id, null), CancellationToken.None));

        attempts.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ListGetAndStateTransitionsRespectOwnershipAndIdempotency()
    {
        User user = User(UserRole.Rider); User other = User(UserRole.Rider); NotificationDeliveryAttempt own = Attempt(user.Id, NotificationDeliveryStatus.Prepared); NotificationDeliveryAttempt otherAttempt = Attempt(other.Id, NotificationDeliveryStatus.Prepared); NotificationService service = Service(user, attempts: new Attempts(own, otherAttempt));
        (await service.ListAsync(user.Id, null, null, null, null, null, CancellationToken.None)).Attempts.Should().ContainSingle(a => a.Id == own.Id);
        await Assert.ThrowsAsync<NotFoundAppException>(() => service.GetAsync(user.Id, otherAttempt.Id, CancellationToken.None));
        MarkNotificationSimulatedSentResponse sent = await service.MarkSimulatedSentAsync(user.Id, own.Id, new MarkNotificationSimulatedSentRequest("sim", "sent"), CancellationToken.None);
        (await service.MarkSimulatedSentAsync(user.Id, own.Id, new MarkNotificationSimulatedSentRequest("sim", "sent"), CancellationToken.None)).Attempt.Status.Should().Be("SimulatedSent");
        sent.Attempt.Status.Should().Be("SimulatedSent");
        NotificationDeliveryAttempt failed = Attempt(user.Id, NotificationDeliveryStatus.Prepared); NotificationService failService = Service(user, attempts: new Attempts(failed));
        (await failService.MarkFailedAsync(user.Id, failed.Id, new MarkNotificationFailedRequest("fail", null), CancellationToken.None)).Attempt.Status.Should().Be("Failed");
        (await failService.MarkFailedAsync(user.Id, failed.Id, new MarkNotificationFailedRequest("fail", null), CancellationToken.None)).Attempt.Status.Should().Be("Failed");
        NotificationDeliveryAttempt cancel = Attempt(user.Id, NotificationDeliveryStatus.Prepared); NotificationService cancelService = Service(user, attempts: new Attempts(cancel));
        (await cancelService.CancelAsync(user.Id, cancel.Id, new CancelNotificationAttemptRequest("cancel", Now), CancellationToken.None)).Attempt.Status.Should().Be("Cancelled");
        (await cancelService.CancelAsync(user.Id, cancel.Id, new CancelNotificationAttemptRequest("cancel", Now), CancellationToken.None)).Attempt.Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task InvalidTransitionsAndNonRidersAreRejectedAndNoProviderDependenciesExist()
    {
        User user = User(UserRole.Rider);
        NotificationDeliveryAttempt failed = Attempt(user.Id, NotificationDeliveryStatus.Failed);
        await Assert.ThrowsAsync<NotificationNotAllowedAppException>(() => Service(user, attempts: new Attempts(failed)).CancelAsync(user.Id, failed.Id, new CancelNotificationAttemptRequest(null, null), CancellationToken.None));
        User monitor = User(UserRole.Monitor); await Assert.ThrowsAsync<ForbiddenAppException>(() => Service(monitor).ListAsync(monitor.Id, null, null, null, null, null, CancellationToken.None));
        User admin = User(UserRole.Admin); await Assert.ThrowsAsync<ForbiddenAppException>(() => Service(admin).ListAsync(admin.Id, null, null, null, null, null, CancellationToken.None));
        typeof(NotificationService).GetConstructors().Single().GetParameters().Select(p => p.ParameterType.Name).Should().NotContain(n => n.Contains("Provider", StringComparison.OrdinalIgnoreCase) || n.Contains("Sms", StringComparison.OrdinalIgnoreCase) || n.Contains("Email", StringComparison.OrdinalIgnoreCase));
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 6, 10, 0, 0, TimeSpan.Zero);
    private static NotificationService Service(User user, bool ready = true, Alerts? alerts = null, Attempts? attempts = null, Contacts? contacts = null, PushTokens? pushTokens = null, Preferences? preferences = null) => new(new Users(user), new StubOnboarding(ready), alerts ?? new Alerts(), attempts ?? new Attempts(), new NotificationIdempotencyKeyFactory(), contacts ?? new Contacts(), pushTokens ?? new PushTokens(), preferences ?? new Preferences(), new Clock());
    private static User User(UserRole role) => new() { Email = $"{Guid.NewGuid()}@example.com", FullName = "Rider", Role = role, IsActive = true };
    private static AlertContactSnapshot Contact(string id, string? phone = null, string? email = null, EmergencyContactInvitationStatus status = EmergencyContactInvitationStatus.Invited) => new() { EmergencyContactId = id, FullName = "Contact", PhoneNumber = phone, Email = email, InvitationStatus = status };
    private static AlertDispatchRequest Alert(string userId, AlertDispatchStatus status, IReadOnlyList<AlertContactSnapshot> contacts) => new() { UserId = userId, IncidentId = "incident", TripId = "trip", VehicleId = "vehicle", MobileDeviceId = "mobile", ClientAlertRequestId = Guid.NewGuid().ToString(), IdempotencyKey = Guid.NewGuid().ToString(), Priority = AlertDispatchPriority.High, Reason = AlertDispatchReason.IncidentCreated, Status = status, RequestedAtUtc = Now, CreatedAtUtc = Now, ContactsSnapshot = contacts };
    private static NotificationDeliveryAttempt Attempt(string userId, NotificationDeliveryStatus status) => new() { UserId = userId, AlertDispatchId = "alert", IncidentId = "incident", TripId = "trip", EmergencyContactId = "contact", Channel = NotificationChannel.Sms, Status = status, Provider = NotificationProvider.None, AttemptNumber = 1, IdempotencyKey = Guid.NewGuid().ToString(), PreparedAtUtc = Now, CreatedAtUtc = Now };
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => Now; }
    private sealed class StubOnboarding(bool ready) : IOnboardingService { public Task<OnboardingStatusResponse> GetStatusAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(ready ? new OnboardingStatusResponse(7, 7, 100, "Completed", true, []) : new OnboardingStatusResponse(7, 6, 86, "Confirmation", false, [])); }
    private sealed class Users(User user) : IUserRepository { public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult<User?>(user.Id == id ? user : null); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult<User?>(null); public Task AddAsync(User user, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(User user, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Alerts(params AlertDispatchRequest[] alerts) : IAlertDispatchRepository { private readonly List<AlertDispatchRequest> _items = alerts.ToList(); public Task<AlertDispatchRequest?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(_items.FirstOrDefault(a => a.Id == id)); public Task<AlertDispatchRequest?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult(_items.FirstOrDefault(a => a.IdempotencyKey == key)); public Task<(AlertDispatchRequest AlertDispatch, bool IsDuplicate)> AddOrGetDuplicateAsync(AlertDispatchRequest alert, CancellationToken ct) => Task.FromResult((alert, false)); public Task<IReadOnlyList<AlertDispatchRequest>> ListByUserIdAsync(string u, AlertDispatchStatus? s, string? i, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertDispatchRequest>>([]); public Task<long> CountByUserIdAsync(string u, AlertDispatchStatus? s, string? i, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(AlertDispatchRequest alert, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Attempts(params NotificationDeliveryAttempt[] attempts) : INotificationDeliveryAttemptRepository { public List<NotificationDeliveryAttempt> Items { get; } = attempts.ToList(); public Task<NotificationDeliveryAttempt?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.Id == id)); public Task<NotificationDeliveryAttempt?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.IdempotencyKey == key)); public Task<(NotificationDeliveryAttempt Attempt, bool IsDuplicate)> AddOrGetDuplicateAsync(NotificationDeliveryAttempt attempt, CancellationToken ct) { NotificationDeliveryAttempt? existing = Items.FirstOrDefault(a => a.IdempotencyKey == attempt.IdempotencyKey); if (existing is not null) return Task.FromResult((existing, true)); Items.Add(attempt); return Task.FromResult((attempt, false)); } public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByUserIdAsync(string u, string? a, string? i, NotificationDeliveryStatus? s, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>(Items.Where(x => x.UserId == u).ToArray()); public Task<long> CountByUserIdAsync(string u, string? a, string? i, NotificationDeliveryStatus? s, CancellationToken ct) => Task.FromResult((long)Items.Count(x => x.UserId == u)); public Task UpdateAsync(NotificationDeliveryAttempt attempt, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Contacts(params EmergencyContact[] contacts) : IEmergencyContactRepository { private readonly List<EmergencyContact> _items = contacts.ToList(); public Task<IReadOnlyList<EmergencyContact>> GetActiveByUserIdAsync(string userId, CancellationToken ct) => Task.FromResult<IReadOnlyList<EmergencyContact>>(_items.Where(c => c.UserId == userId && c.IsActive).ToArray()); public Task<EmergencyContact?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(_items.FirstOrDefault(c => c.Id == id)); public Task<EmergencyContact?> GetByLinkingCodeAsync(string linkingCode, CancellationToken ct) => Task.FromResult<EmergencyContact?>(null); public Task<int> CountActiveByUserIdAsync(string userId, CancellationToken ct) => Task.FromResult(0); public Task AddAsync(EmergencyContact contact, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(EmergencyContact contact, CancellationToken ct) => Task.CompletedTask; }
    private sealed class PushTokens(params PushNotificationToken[] tokens) : IPushNotificationTokenRepository { private readonly List<PushNotificationToken> _items = tokens.ToList(); public Task<PushNotificationToken?> GetLatestActiveFcmByUserIdAsync(string userId, CancellationToken ct) => Task.FromResult(_items.Where(t => t.UserId == userId && t.Status == PushNotificationTokenStatus.Active && t.Channel == PushTokenChannel.Fcm && t.Platform is PushTokenPlatform.Android or PushTokenPlatform.Web).OrderByDescending(t => t.LastSeenAtUtc).FirstOrDefault()); public Task<PushNotificationToken?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult<PushNotificationToken?>(null); public Task<PushNotificationToken?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) => Task.FromResult<PushNotificationToken?>(null); public Task<(PushNotificationToken Token, bool IsDuplicate)> AddOrGetDuplicateAsync(PushNotificationToken token, CancellationToken ct) => Task.FromResult((token, false)); public Task UpdateAsync(PushNotificationToken token, CancellationToken ct) => Task.CompletedTask; public Task<long> RevokeActiveTokensForScopeAsync(string userId, PushTokenPlatform platform, PushTokenChannel channel, string? deviceId, DateTimeOffset now, CancellationToken ct) => Task.FromResult(0L); public Task<IReadOnlyList<PushNotificationToken>> ListByUserIdAsync(string userId, PushNotificationTokenQuery query, CancellationToken ct) => Task.FromResult<IReadOnlyList<PushNotificationToken>>([]); public Task<long> CountByUserIdAsync(string userId, PushNotificationTokenQuery query, CancellationToken ct) => Task.FromResult(0L); public Task<PushNotificationTokenStatusSummary> GetStatusByUserIdAsync(string userId, CancellationToken ct) => Task.FromResult(new PushNotificationTokenStatusSummary(0, 0, false, false, false, false, null)); public Task<IReadOnlyList<PushNotificationToken>> ListAsync(PushNotificationTokenQuery query, CancellationToken ct) => Task.FromResult<IReadOnlyList<PushNotificationToken>>([]); public Task<long> CountAsync(PushNotificationTokenQuery query, CancellationToken ct) => Task.FromResult(0L); }
    private sealed class Preferences(params NotificationPreference[] preferences) : INotificationPreferenceRepository { private readonly List<NotificationPreference> _items = preferences.ToList(); public Task<NotificationPreference?> GetByUserIdAsync(string userId, CancellationToken ct) => Task.FromResult(_items.FirstOrDefault(p => p.UserId == userId)); public Task AddAsync(NotificationPreference preference, CancellationToken ct) { _items.Add(preference); return Task.CompletedTask; } public Task UpdateAsync(NotificationPreference preference, CancellationToken ct) => Task.CompletedTask; }
}
