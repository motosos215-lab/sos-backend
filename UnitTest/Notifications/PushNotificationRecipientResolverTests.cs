using FluentAssertions;
using MotoSOS.API.Modules.EmergencyContacts.Application;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
using MotoSOS.API.Modules.Notifications.Application;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.Notifications.Providers;
using MotoSOS.API.Modules.PushNotificationTokens.Application;
using MotoSOS.API.Modules.PushNotificationTokens.Domain;

namespace UnitTest.Notifications;

public sealed class PushNotificationRecipientResolverTests
{
    [Fact]
    public async Task ResolvesLatestActiveFcmTokenForLinkedMonitorOnly()
    {
        var attempts = new Attempts(new NotificationDeliveryAttempt { Id = "attempt", UserId = "rider", EmergencyContactId = "contact", Channel = NotificationChannel.Push });
        var contacts = new Contacts(new EmergencyContact { Id = "contact", UserId = "rider", IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Linked, LinkedUserId = "monitor" });
        var older = Token("monitor", "older", PushNotificationTokenStatus.Active, Now.AddMinutes(-2));
        var latest = Token("monitor", "latest", PushNotificationTokenStatus.Active, Now);
        var tokens = new Tokens([older, latest, Token("other", "other", PushNotificationTokenStatus.Active, Now.AddMinutes(1)), Token("monitor", "revoked", PushNotificationTokenStatus.Revoked, Now.AddMinutes(2))]);

        PushNotificationRecipientResolution resolution = await new PushNotificationRecipientResolver(attempts, contacts, tokens).ResolveAsync("attempt", CancellationToken.None);

        resolution.Recipient.Should().NotBeNull();
        resolution.Recipient!.UserId.Should().Be("monitor");
        resolution.Recipient.Token.Id.Should().Be("latest");
    }

    [Fact]
    public async Task MissingLinkedUserOrTokenFailsControlled()
    {
        var attempts = new Attempts(new NotificationDeliveryAttempt { Id = "attempt", UserId = "rider", EmergencyContactId = "contact", Channel = NotificationChannel.Push });
        var contacts = new Contacts(new EmergencyContact { Id = "contact", UserId = "rider", IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Linked });

        PushNotificationRecipientResolution noRecipient = await new PushNotificationRecipientResolver(attempts, contacts, new Tokens([])).ResolveAsync("attempt", CancellationToken.None);

        noRecipient.FailureCode.Should().Be("push_recipient_not_available");

        contacts.Items[0].LinkedUserId = "monitor";
        PushNotificationRecipientResolution noToken = await new PushNotificationRecipientResolver(attempts, contacts, new Tokens([])).ResolveAsync("attempt", CancellationToken.None);
        noToken.FailureCode.Should().Be("push_token_not_available");
    }

    [Fact]
    public async Task FeedbackAttemptWithEventTypeAndRecipientUserIdResolvesDirectRiderOnly()
    {
        var attempts = new Attempts(new NotificationDeliveryAttempt { Id = "feedback", UserId = "rider", RecipientUserId = "rider", EventType = NotificationEventTypes.MonitorAlertViewed, EmergencyContactId = "contact", Channel = NotificationChannel.Push });
        var contacts = new Contacts(new EmergencyContact { Id = "contact", UserId = "rider", IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Linked, LinkedUserId = "monitor" });
        var tokens = new Tokens([Token("monitor", "monitor-token", PushNotificationTokenStatus.Active, Now), Token("other-rider", "other-rider-token", PushNotificationTokenStatus.Active, Now.AddMinutes(1)), Token("rider", "rider-token", PushNotificationTokenStatus.Active, Now.AddMinutes(2))]);

        PushNotificationRecipientResolution resolution = await new PushNotificationRecipientResolver(attempts, contacts, tokens).ResolveAsync("feedback", CancellationToken.None);

        resolution.Recipient.Should().NotBeNull();
        resolution.Recipient!.UserId.Should().Be("rider");
        resolution.Recipient.Token.Id.Should().Be("rider-token");
    }

    [Fact]
    public async Task RecipientUserIdWithoutEventTypeKeepsOriginalMonitorResolution()
    {
        var attempts = new Attempts(new NotificationDeliveryAttempt { Id = "attempt", UserId = "rider", RecipientUserId = "rider", EmergencyContactId = "contact", Channel = NotificationChannel.Push });
        var contacts = new Contacts(new EmergencyContact { Id = "contact", UserId = "rider", IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Linked, LinkedUserId = "monitor" });
        var tokens = new Tokens([Token("rider", "rider-token", PushNotificationTokenStatus.Active, Now.AddMinutes(1)), Token("monitor", "monitor-token", PushNotificationTokenStatus.Active, Now)]);

        PushNotificationRecipientResolution resolution = await new PushNotificationRecipientResolver(attempts, contacts, tokens).ResolveAsync("attempt", CancellationToken.None);

        resolution.Recipient.Should().NotBeNull();
        resolution.Recipient!.UserId.Should().Be("monitor");
        resolution.Recipient.Token.Id.Should().Be("monitor-token");
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);
    private static PushNotificationToken Token(string userId, string id, PushNotificationTokenStatus status, DateTimeOffset lastSeen) => new() { Id = id, UserId = userId, Channel = PushTokenChannel.Fcm, Platform = PushTokenPlatform.Android, Status = status, LastSeenAtUtc = lastSeen, TokenValue = $"value-{id}" };
    private sealed class Attempts(params NotificationDeliveryAttempt[] items) : INotificationDeliveryAttemptRepository { public Task<NotificationDeliveryAttempt?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(items.FirstOrDefault(i => i.Id == id)); public Task<NotificationDeliveryAttempt?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) => Task.FromResult<NotificationDeliveryAttempt?>(null); public Task<(NotificationDeliveryAttempt Attempt, bool IsDuplicate)> AddOrGetDuplicateAsync(NotificationDeliveryAttempt attempt, CancellationToken ct) => Task.FromResult((attempt, false)); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByUserIdAsync(string userId, string? alertDispatchId, string? incidentId, NotificationDeliveryStatus? status, int pageNumber, int pageSize, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>([]); public Task<long> CountByUserIdAsync(string userId, string? alertDispatchId, string? incidentId, NotificationDeliveryStatus? status, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(NotificationDeliveryAttempt attempt, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Contacts(params EmergencyContact[] items) : IEmergencyContactRepository { public List<EmergencyContact> Items { get; } = items.ToList(); public Task<IReadOnlyList<EmergencyContact>> GetActiveByUserIdAsync(string userId, CancellationToken ct) => Task.FromResult<IReadOnlyList<EmergencyContact>>([]); public Task<EmergencyContact?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id)); public Task<EmergencyContact?> GetByLinkingCodeAsync(string linkingCode, CancellationToken ct) => Task.FromResult<EmergencyContact?>(null); public Task<int> CountActiveByUserIdAsync(string userId, CancellationToken ct) => Task.FromResult(0); public Task AddAsync(EmergencyContact contact, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(EmergencyContact contact, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Tokens(IReadOnlyList<PushNotificationToken> items) : IPushNotificationTokenRepository { public Task<PushNotificationToken?> GetLatestActiveFcmByUserIdAsync(string userId, CancellationToken ct) => Task.FromResult(items.Where(t => t.UserId == userId && t.Status == PushNotificationTokenStatus.Active && t.Channel == PushTokenChannel.Fcm && t.Platform is PushTokenPlatform.Android or PushTokenPlatform.Web).OrderByDescending(t => t.LastSeenAtUtc).FirstOrDefault()); public Task<PushNotificationToken?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult<PushNotificationToken?>(null); public Task<PushNotificationToken?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) => Task.FromResult<PushNotificationToken?>(null); public Task<(PushNotificationToken Token, bool IsDuplicate)> AddOrGetDuplicateAsync(PushNotificationToken token, CancellationToken ct) => Task.FromResult((token, false)); public Task UpdateAsync(PushNotificationToken token, CancellationToken ct) => Task.CompletedTask; public Task<long> RevokeActiveTokensForScopeAsync(string userId, PushTokenPlatform platform, PushTokenChannel channel, string? deviceId, DateTimeOffset now, CancellationToken ct) => Task.FromResult(0L); public Task<IReadOnlyList<PushNotificationToken>> ListByUserIdAsync(string userId, PushNotificationTokenQuery query, CancellationToken ct) => Task.FromResult<IReadOnlyList<PushNotificationToken>>([]); public Task<long> CountByUserIdAsync(string userId, PushNotificationTokenQuery query, CancellationToken ct) => Task.FromResult(0L); public Task<PushNotificationTokenStatusSummary> GetStatusByUserIdAsync(string userId, CancellationToken ct) => Task.FromResult(new PushNotificationTokenStatusSummary(0, 0, false, false, false, false, null)); public Task<IReadOnlyList<PushNotificationToken>> ListAsync(PushNotificationTokenQuery query, CancellationToken ct) => Task.FromResult<IReadOnlyList<PushNotificationToken>>([]); public Task<long> CountAsync(PushNotificationTokenQuery query, CancellationToken ct) => Task.FromResult(0L); }
}
