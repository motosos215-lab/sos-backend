using FluentAssertions;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AlertDispatch.Application;
using MotoSOS.API.Modules.AlertDispatch.Domain;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
using MotoSOS.API.Modules.Notifications.Application;
using MotoSOS.API.Modules.Notifications.Contracts;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.Onboarding.Application;
using MotoSOS.API.Modules.Onboarding.Contracts;
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
        typeof(NotificationService).GetConstructors().Single().GetParameters().Select(p => p.ParameterType.Name).Should().NotContain(n => n.Contains("Provider", StringComparison.OrdinalIgnoreCase) || n.Contains("Sms", StringComparison.OrdinalIgnoreCase) || n.Contains("Email", StringComparison.OrdinalIgnoreCase) || n.Contains("Push", StringComparison.OrdinalIgnoreCase));
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 6, 10, 0, 0, TimeSpan.Zero);
    private static NotificationService Service(User user, bool ready = true, Alerts? alerts = null, Attempts? attempts = null) => new(new Users(user), new StubOnboarding(ready), alerts ?? new Alerts(), attempts ?? new Attempts(), new NotificationIdempotencyKeyFactory(), new Clock());
    private static User User(UserRole role) => new() { Email = $"{Guid.NewGuid()}@example.com", FullName = "Rider", Role = role, IsActive = true };
    private static AlertContactSnapshot Contact(string id, string? phone = null, string? email = null) => new() { EmergencyContactId = id, FullName = "Contact", PhoneNumber = phone, Email = email, InvitationStatus = EmergencyContactInvitationStatus.Invited };
    private static AlertDispatchRequest Alert(string userId, AlertDispatchStatus status, IReadOnlyList<AlertContactSnapshot> contacts) => new() { UserId = userId, IncidentId = "incident", TripId = "trip", VehicleId = "vehicle", MobileDeviceId = "mobile", ClientAlertRequestId = Guid.NewGuid().ToString(), IdempotencyKey = Guid.NewGuid().ToString(), Priority = AlertDispatchPriority.High, Reason = AlertDispatchReason.IncidentCreated, Status = status, RequestedAtUtc = Now, CreatedAtUtc = Now, ContactsSnapshot = contacts };
    private static NotificationDeliveryAttempt Attempt(string userId, NotificationDeliveryStatus status) => new() { UserId = userId, AlertDispatchId = "alert", IncidentId = "incident", TripId = "trip", EmergencyContactId = "contact", Channel = NotificationChannel.Sms, Status = status, Provider = NotificationProvider.None, AttemptNumber = 1, IdempotencyKey = Guid.NewGuid().ToString(), PreparedAtUtc = Now, CreatedAtUtc = Now };
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => Now; }
    private sealed class StubOnboarding(bool ready) : IOnboardingService { public Task<OnboardingStatusResponse> GetStatusAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(ready ? new OnboardingStatusResponse(7, 7, 100, "Completed", true, []) : new OnboardingStatusResponse(7, 6, 86, "Confirmation", false, [])); }
    private sealed class Users(User user) : IUserRepository { public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult<User?>(user.Id == id ? user : null); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult<User?>(null); public Task AddAsync(User user, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(User user, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Alerts(params AlertDispatchRequest[] alerts) : IAlertDispatchRepository { private readonly List<AlertDispatchRequest> _items = alerts.ToList(); public Task<AlertDispatchRequest?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(_items.FirstOrDefault(a => a.Id == id)); public Task<AlertDispatchRequest?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult(_items.FirstOrDefault(a => a.IdempotencyKey == key)); public Task<(AlertDispatchRequest AlertDispatch, bool IsDuplicate)> AddOrGetDuplicateAsync(AlertDispatchRequest alert, CancellationToken ct) => Task.FromResult((alert, false)); public Task<IReadOnlyList<AlertDispatchRequest>> ListByUserIdAsync(string u, AlertDispatchStatus? s, string? i, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertDispatchRequest>>([]); public Task<long> CountByUserIdAsync(string u, AlertDispatchStatus? s, string? i, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(AlertDispatchRequest alert, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Attempts(params NotificationDeliveryAttempt[] attempts) : INotificationDeliveryAttemptRepository { public List<NotificationDeliveryAttempt> Items { get; } = attempts.ToList(); public Task<NotificationDeliveryAttempt?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.Id == id)); public Task<NotificationDeliveryAttempt?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.IdempotencyKey == key)); public Task<(NotificationDeliveryAttempt Attempt, bool IsDuplicate)> AddOrGetDuplicateAsync(NotificationDeliveryAttempt attempt, CancellationToken ct) { NotificationDeliveryAttempt? existing = Items.FirstOrDefault(a => a.IdempotencyKey == attempt.IdempotencyKey); if (existing is not null) return Task.FromResult((existing, true)); Items.Add(attempt); return Task.FromResult((attempt, false)); } public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByUserIdAsync(string u, string? a, string? i, NotificationDeliveryStatus? s, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>(Items.Where(x => x.UserId == u).ToArray()); public Task<long> CountByUserIdAsync(string u, string? a, string? i, NotificationDeliveryStatus? s, CancellationToken ct) => Task.FromResult((long)Items.Count(x => x.UserId == u)); public Task UpdateAsync(NotificationDeliveryAttempt attempt, CancellationToken ct) => Task.CompletedTask; }
}
