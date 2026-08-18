using FluentAssertions;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Modules.AlertAcknowledgements.Domain;
using MotoSOS.API.Modules.NotificationPreferences.Application;
using MotoSOS.API.Modules.NotificationPreferences.Domain;
using MotoSOS.API.Modules.Notifications.Application;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.PushNotificationTokens.Application;
using MotoSOS.API.Modules.PushNotificationTokens.Domain;

namespace UnitTest.Notifications;

public sealed class RiderAlertFeedbackNotificationServiceTests
{
    [Fact]
    public async Task EnqueuesDirectRiderFeedbackAttemptWithRequiredMetadata()
    {
        var attempts = new Attempts();
        var service = Service(attempts, new Preferences(), new Tokens(Token("rider")));

        await service.EnqueueAsync(Ack(), NotificationEventTypes.MonitorAlertViewed, Now, CancellationToken.None);

        NotificationDeliveryAttempt feedback = attempts.Items.Should().ContainSingle().Subject;
        feedback.UserId.Should().Be("rider");
        feedback.RecipientUserId.Should().Be("rider");
        feedback.EventType.Should().Be(NotificationEventTypes.MonitorAlertViewed);
        feedback.MonitorAlertAttemptId.Should().Be("monitor-attempt");
        feedback.MonitorUserId.Should().Be("monitor");
        feedback.Screen.Should().Be(NotificationScreens.EmergencyStatus);
        feedback.OccurredAtUtc.Should().Be(Now);
        feedback.Status.Should().Be(NotificationDeliveryStatus.Prepared);
        feedback.Channel.Should().Be(NotificationChannel.Push);
    }

    [Fact]
    public async Task DuplicateFeedbackKeyDoesNotCreateSecondAttempt()
    {
        var attempts = new Attempts();
        var service = Service(attempts, new Preferences(), new Tokens(Token("rider")));

        await service.EnqueueAsync(Ack(), NotificationEventTypes.MonitorAlertAcknowledged, Now, CancellationToken.None);
        await service.EnqueueAsync(Ack(), NotificationEventTypes.MonitorAlertAcknowledged, Now.AddMinutes(1), CancellationToken.None);

        attempts.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task PushDisabledOrMissingRiderFcmSkipsWithoutThrowing()
    {
        var disabledAttempts = new Attempts();
        await Service(disabledAttempts, new Preferences(new NotificationPreference { UserId = "rider", PushEnabled = false }), new Tokens(Token("rider"))).EnqueueAsync(Ack(), NotificationEventTypes.MonitorAlertDeclined, Now, CancellationToken.None);

        var noTokenAttempts = new Attempts();
        await Service(noTokenAttempts, new Preferences(), new Tokens()).EnqueueAsync(Ack(), NotificationEventTypes.MonitorAlertDeclined, Now, CancellationToken.None);

        disabledAttempts.Items.Should().BeEmpty();
        noTokenAttempts.Items.Should().BeEmpty();
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 18, 10, 30, 0, TimeSpan.Zero);
    private static RiderAlertFeedbackNotificationService Service(Attempts attempts, Preferences preferences, Tokens tokens) => new(attempts, preferences, tokens, new RiderAlertFeedbackNotificationIdempotencyKeyFactory(), new Clock());
    private static AlertAcknowledgement Ack() => new() { UserId = "rider", MonitorUserId = "monitor", EmergencyContactId = "contact", AlertDispatchId = "alert", NotificationDeliveryAttemptId = "monitor-attempt", IncidentId = "incident", TripId = "trip" };
    private static PushNotificationToken Token(string userId) => new() { UserId = userId, Channel = PushTokenChannel.Fcm, Platform = PushTokenPlatform.Android, Status = PushNotificationTokenStatus.Active, TokenValue = "internal", LastSeenAtUtc = Now };
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => Now; }
    private sealed class Attempts : INotificationDeliveryAttemptRepository { public List<NotificationDeliveryAttempt> Items { get; } = []; public Task<NotificationDeliveryAttempt?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.Id == id)); public Task<NotificationDeliveryAttempt?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.IdempotencyKey == key)); public Task<(NotificationDeliveryAttempt Attempt, bool IsDuplicate)> AddOrGetDuplicateAsync(NotificationDeliveryAttempt attempt, CancellationToken ct) { NotificationDeliveryAttempt? existing = Items.FirstOrDefault(a => a.IdempotencyKey == attempt.IdempotencyKey); if (existing is not null) return Task.FromResult((existing, true)); Items.Add(attempt); return Task.FromResult((attempt, false)); } public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByUserIdAsync(string u, string? a, string? i, NotificationDeliveryStatus? s, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>([]); public Task<long> CountByUserIdAsync(string u, string? a, string? i, NotificationDeliveryStatus? s, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(NotificationDeliveryAttempt attempt, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Preferences(params NotificationPreference[] preferences) : INotificationPreferenceRepository { public Task<NotificationPreference?> GetByUserIdAsync(string userId, CancellationToken ct) => Task.FromResult(preferences.FirstOrDefault(p => p.UserId == userId)); public Task AddAsync(NotificationPreference preference, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(NotificationPreference preference, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Tokens(params PushNotificationToken[] tokens) : IPushNotificationTokenRepository { public Task<PushNotificationToken?> GetLatestActiveFcmByUserIdAsync(string userId, CancellationToken ct) => Task.FromResult(tokens.Where(t => t.UserId == userId && t.Status == PushNotificationTokenStatus.Active && t.Channel == PushTokenChannel.Fcm).OrderByDescending(t => t.LastSeenAtUtc).FirstOrDefault()); public Task<PushNotificationToken?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult<PushNotificationToken?>(null); public Task<PushNotificationToken?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) => Task.FromResult<PushNotificationToken?>(null); public Task<(PushNotificationToken Token, bool IsDuplicate)> AddOrGetDuplicateAsync(PushNotificationToken token, CancellationToken ct) => Task.FromResult((token, false)); public Task UpdateAsync(PushNotificationToken token, CancellationToken ct) => Task.CompletedTask; public Task<long> RevokeActiveTokensForScopeAsync(string userId, PushTokenPlatform platform, PushTokenChannel channel, string? deviceId, DateTimeOffset now, CancellationToken ct) => Task.FromResult(0L); public Task<IReadOnlyList<PushNotificationToken>> ListByUserIdAsync(string userId, PushNotificationTokenQuery query, CancellationToken ct) => Task.FromResult<IReadOnlyList<PushNotificationToken>>([]); public Task<long> CountByUserIdAsync(string userId, PushNotificationTokenQuery query, CancellationToken ct) => Task.FromResult(0L); public Task<PushNotificationTokenStatusSummary> GetStatusByUserIdAsync(string userId, CancellationToken ct) => Task.FromResult(new PushNotificationTokenStatusSummary(0, 0, false, false, false, false, null)); public Task<IReadOnlyList<PushNotificationToken>> ListAsync(PushNotificationTokenQuery query, CancellationToken ct) => Task.FromResult<IReadOnlyList<PushNotificationToken>>([]); public Task<long> CountAsync(PushNotificationTokenQuery query, CancellationToken ct) => Task.FromResult(0L); }
}
