using FluentAssertions;
using Microsoft.Extensions.Options;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Modules.Notifications.Providers;
using MotoSOS.API.Modules.PushNotificationTokens.Domain;

namespace UnitTest.Notifications;

public sealed class FcmNotificationProviderTests
{
    [Fact]
    public async Task EnabledWithoutConfigurationFailsControlledAndDoesNotCallClient()
    {
        var client = new Client(new FcmPushResult(true, "message", null, null));
        FcmNotificationProvider provider = Create(client, Recipient(), new FcmNotificationProviderOptions { Enabled = true });

        NotificationProviderResult result = await provider.SendAsync(Request(), CancellationToken.None);

        result.DeliveryStatus.Should().Be(NotificationProviderDeliveryStatus.Failed);
        result.ErrorCode.Should().Be("provider_not_configured");
        client.Calls.Should().Be(0);
    }

    [Fact]
    public async Task MissingRecipientOrTokenFailsControlled()
    {
        FcmNotificationProvider provider = Create(new Client(new FcmPushResult(true, "message", null, null)), PushNotificationRecipientResolution.Failed("push_token_not_available"), Configured());

        NotificationProviderResult result = await provider.SendAsync(Request(), CancellationToken.None);

        result.DeliveryStatus.Should().Be(NotificationProviderDeliveryStatus.Failed);
        result.ErrorCode.Should().Be("push_token_not_available");
    }

    [Fact]
    public async Task SuccessfulSendUsesTokenValueInternallyAndReturnsSafeProviderResult()
    {
        var client = new Client(new FcmPushResult(true, "projects/demo/messages/123", null, null));
        FcmNotificationProvider provider = Create(client, Recipient(), Configured());

        NotificationProviderResult result = await provider.SendAsync(Request(), CancellationToken.None);

        result.ProviderType.Should().Be(NotificationProviderType.Fcm);
        result.DeliveryStatus.Should().Be(NotificationProviderDeliveryStatus.Sent);
        result.ProviderMessageId.Should().Be("projects/demo/messages/123");
        client.Calls.Should().Be(1);
        client.LastRequest!.RecipientToken.Should().Be("internal-token-value");
    }

    [Fact]
    public async Task ClientFailureReturnsSanitizedFailureCode()
    {
        FcmNotificationProvider provider = Create(new Client(new FcmPushResult(false, null, "Invalid Token", "raw hidden")), Recipient(), Configured());

        NotificationProviderResult result = await provider.SendAsync(Request(), CancellationToken.None);

        result.DeliveryStatus.Should().Be(NotificationProviderDeliveryStatus.Failed);
        result.ErrorCode.Should().Be("invalid_token");
        result.ErrorMessage.Should().NotContain("raw hidden");
    }

    private static NotificationProviderRequest Request() => new("attempt", "alert", "incident", NotificationProviderChannel.Push, false);
    private static FcmNotificationProviderOptions Configured() => new() { Enabled = true, ProjectId = "project", ServiceAccountJson = "{}" };
    private static PushNotificationRecipientResolution Recipient() => PushNotificationRecipientResolution.Success(new PushNotificationRecipient("monitor", new PushNotificationToken { TokenValue = "internal-token-value", Channel = PushTokenChannel.Fcm, Platform = PushTokenPlatform.Android, Status = PushNotificationTokenStatus.Active }));
    private static FcmNotificationProvider Create(IFcmPushClient client, PushNotificationRecipientResolution recipient, FcmNotificationProviderOptions options) => new(client, new Recipients(recipient), new FcmNotificationMessageFactory(Options.Create(options)), new FcmNotificationProviderOptionsValidator(), Options.Create(options), new Clock());
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero); }
    private sealed class Client(FcmPushResult result) : IFcmPushClient { public int Calls { get; private set; } public FcmPushRequest? LastRequest { get; private set; } public Task<FcmPushResult> SendAsync(FcmPushRequest request, CancellationToken ct) { Calls++; LastRequest = request; return Task.FromResult(result); } }
    private sealed class Recipients(PushNotificationRecipientResolution result) : IPushNotificationRecipientResolver { public Task<PushNotificationRecipientResolution> ResolveAsync(string notificationDeliveryAttemptId, CancellationToken cancellationToken) => Task.FromResult(result); }
}
