using FluentAssertions;
using Microsoft.Extensions.Options;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.PushNotificationTokens.Domain;
using MotoSOS.API.Modules.Notifications.Providers;

namespace UnitTest.Notifications;

public sealed class NotificationProviderResolverTests
{
    [Theory]
    [InlineData(NotificationProviderChannel.Sms)]
    [InlineData(NotificationProviderChannel.Email)]
    [InlineData(NotificationProviderChannel.Push)]
    public void SupportedChannelsResolveToSimulatedProviderWhenFcmDisabled(NotificationProviderChannel channel)
    {
        var resolver = new NotificationProviderResolver(new SimulatedNotificationProvider(new Clock()), FcmProvider(false), Options.Create(new FcmNotificationProviderOptions { Enabled = false }));
        INotificationProvider provider = resolver.Resolve(channel);
        provider.Should().BeOfType<SimulatedNotificationProvider>();
        provider.ProviderType.Should().Be(NotificationProviderType.Simulated);
    }

    [Fact]
    public void PushResolvesToFcmProviderWhenEnabled()
    {
        var resolver = new NotificationProviderResolver(new SimulatedNotificationProvider(new Clock()), FcmProvider(true), Options.Create(new FcmNotificationProviderOptions { Enabled = true }));

        INotificationProvider provider = resolver.Resolve(NotificationProviderChannel.Push);

        provider.ProviderType.Should().Be(NotificationProviderType.Fcm);
    }

    [Fact]
    public void UnsupportedChannelThrowsControlledError()
    {
        var resolver = new NotificationProviderResolver(new SimulatedNotificationProvider(new Clock()));
        Assert.Throws<NotificationNotAllowedAppException>(() => resolver.Resolve((NotificationProviderChannel)99));
    }

    private sealed class Clock : IClock { public DateTimeOffset UtcNow => new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero); }
    private static FcmNotificationProvider FcmProvider(bool enabled) => new(new Client(), new Recipients(), new FcmNotificationMessageFactory(Options.Create(new FcmNotificationProviderOptions { Enabled = enabled, ProjectId = "project", ServiceAccountJson = "{}" })), new FcmNotificationProviderOptionsValidator(), Options.Create(new FcmNotificationProviderOptions { Enabled = enabled, ProjectId = "project", ServiceAccountJson = "{}" }), new Clock());
    private sealed class Client : IFcmPushClient { public Task<FcmPushResult> SendAsync(FcmPushRequest request, CancellationToken cancellationToken) => Task.FromResult(new FcmPushResult(true, "message", null, null)); }
    private sealed class Recipients : IPushNotificationRecipientResolver { public Task<PushNotificationRecipientResolution> ResolveAsync(string notificationDeliveryAttemptId, CancellationToken cancellationToken) => Task.FromResult(PushNotificationRecipientResolution.Success(new PushNotificationRecipient("monitor", new PushNotificationToken { TokenValue = "token" }))); }
}
